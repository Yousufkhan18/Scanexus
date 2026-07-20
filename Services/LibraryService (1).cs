using LibrarySystem.Data;
using LibrarySystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace LibrarySystem.Services
{
    public interface ILibraryService
    {
        Task<(bool Success, string Message, Student? Student)> AuthenticateAsync(string universityId, string password);
        Task<(bool Success, string Message, Transaction? Txn)> IssueBookAsync(string universityId, string qrCode);
        Task<(bool Success, string Message)> ReturnBookAsync(string txnCode);
        Task<(bool Success, string Message)> ReturnBookByQRAsync(string returnQrCode, string universityId);
        Task<(Book? Book, Transaction? ActiveTxn)> GetBookWithTxnAsync(string qrCode, string universityId);
        Task<(bool Success, string Message)> UpdateDueDateAsync(int transactionId, DateTime newDueDate);
        Task<DashboardViewModel?> GetDashboardAsync(string universityId);
        Task<List<Book>> GetAllBooksAsync();
        Task<Book?> GetBookByQRAsync(string qrCode);
        Task<List<Transaction>> GetAllTransactionsAsync();
        Task<string> GenerateQRCodeBase64Async(string content);
        Task<int> GetActiveBorrowersCountAsync();
        Task<List<Transaction>> GetTodayTransactionsAsync();
        Task<List<Transaction>> GetDefaultersAsync();
        Task<(decimal TotalCalculated, decimal TotalPaid, decimal TotalOutstanding)> GetFineSummaryAsync();
        Task<List<Transaction>> GetFineDetailsAsync();
        Task<List<(string Title, string Author, int Count)>> GetMostBorrowedBooksAsync();
        Task<List<Transaction>> GetCirculationLogAsync(DateTime? from, DateTime? to);
        Task<(int TotalCopies, int AvailableCopies, int IssuedCopies)> GetInventoryStatusAsync();
        Task LogActivityAsync(string actionType, string? userId, string? userName, string? details, string? ipAddress = null);
        Task<List<ActivityLog>> GetActivityLogsAsync(string? actionType = null, int take = 100);
        Task<List<Book>> GetAIRecommendationsAsync(string universityId);
        Task<List<(string FullName, string UniversityID, int BorrowCount)>> GetMostActiveStudentsAsync();
        Task<List<(int Hour, int Count)>> GetPeakIssuingTimingsAsync();
        Task<List<(string Month, decimal TotalFine)>> GetFineTrendsAsync();
        Task<List<(string Batch, int StudentCount, int TotalBorrows)>> GetBatchWiseStatsAsync();

        // 📦 Quick stock adjustment contract signature
        Task<(bool Success, string Message)> AddBookCopiesAsync(int bookId, int additionalCopies);
    }

    // 🧠 ML.NET INTERNAL STRUCTURAL CLASSES
    public class BookBorrowEvent
    {
        [KeyType(count: 10000)]
        public uint StudentKey { get; set; }

        [KeyType(count: 10000)]
        public uint BookKey { get; set; }

        public float Label { get; set; }
    }

    public class BookPrediction
    {
        public float Score { get; set; }
    }

    public class LibraryService : ILibraryService
    {
        private readonly LibraryDbContext _db;

        public LibraryService(LibraryDbContext db) => _db = db;

        public async Task<(bool, string, Student?)> AuthenticateAsync(string universityId, string password)
        {
            var hashedPassword = HashPassword(password);

            var student = await _db.Students
                .FirstOrDefaultAsync(s => s.UniversityID == universityId && s.PasswordHash == hashedPassword);

            if (student == null)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, null, "Login failed: Invalid credentials.");
                return (false, "Invalid University ID or password.", null);
            }

            if (!student.IsActive)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, student.FullName, "Login blocked: Account is inactive.");
                return (false, "Your account is inactive. Contact the library.", null);
            }

            await LogActivityAsync("LOGIN", student.UniversityID, student.FullName, "Student authenticated successfully.");

            return (true, "Login successful.", student);
        }

        public async Task<(bool, string, Transaction?)> IssueBookAsync(string universityId, string qrCode)
        {
            var student = await _db.Students
                .FirstOrDefaultAsync(s => s.UniversityID == universityId);

            if (student == null)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, null, "Issue failed: Student not found");
                return (false, "Student not found.", null);
            }
            if (!student.IsActive)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, student.FullName, "Issue failed: Inactive student");
                return (false, "Inactive students cannot borrow books.", null);
            }

            var book = await _db.Books.FirstOrDefaultAsync(b => b.QRCode == qrCode);
            if (book == null)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, student.FullName, "Issue failed: Invalid QR code");
                return (false, "Invalid QR code. Book not found.", null);
            }

            var alreadyIssued = await _db.Transactions
                .AnyAsync(t => t.StudentID == student.StudentID
                            && t.BookID == book.BookID
                            && t.Status == "Active");
            if (alreadyIssued)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, student.FullName, $"Issue failed: Already has '{book.Title}'");
                return (false, "You already have this book issued.", null);
            }

            if (book.AvailableCopies <= 0)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, student.FullName, $"Issue failed: '{book.Title}' unavailable");
                return (false, "All copies of this book are currently issued.", null);
            }

            var activeCount = await _db.Transactions
                .CountAsync(t => t.StudentID == student.StudentID && t.Status == "Active");
            if (activeCount >= 3)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, student.FullName, "Issue failed: Borrowing limit reached");
                return (false, "Borrowing limit reached. Return a book before issuing another.", null);
            }

            using var txn = await _db.Database.BeginTransactionAsync();
            try
            {
                var newTxn = new Transaction
                {
                    TxnCode = GenerateTxnCode(),
                    StudentID = student.StudentID,
                    BookID = book.BookID,
                    IssueDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(14),
                    Status = "Active",
                    QRScanData = qrCode
                };

                _db.Transactions.Add(newTxn);
                book.AvailableCopies--;

                await _db.SaveChangesAsync();
                await txn.CommitAsync();

                newTxn.Student = student;
                newTxn.Book = book;

                await LogActivityAsync("BOOK_ISSUE", universityId, student.FullName, $"Issued '{book.Title}' — TxnCode: {newTxn.TxnCode}");

                return (true, "Book issued successfully! Due in 14 days.", newTxn);
            }
            catch (Exception ex)
            {
                await txn.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}", null);
            }
        }

        public async Task<(bool, string)> ReturnBookAsync(string txnCode)
        {
            var txn = await _db.Transactions
                .Include(t => t.Book)
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.TxnCode == txnCode);

            if (txn == null) return (false, "Transaction not found.");
            if (txn.Status == "Returned") return (false, "Book already returned.");

            txn.ReturnDate = DateTime.Now;

            if (txn.DueDate < DateTime.Now)
            {
                int overdueDays = (DateTime.Now - txn.DueDate).Days;
                txn.FineAmount = overdueDays * 20;
                txn.FinePaid = true;
                txn.Status = "Returned";
                txn.Book!.AvailableCopies++;
                await _db.SaveChangesAsync();

                await LogActivityAsync("BOOK_RETURN", txn.Student?.UniversityID, txn.Student?.FullName,
                    $"Returned '{txn.Book.Title}' — Overdue {overdueDays} day(s), Fine: Rs. {txn.FineAmount}");

                await LogActivityAsync("FINE_PAYMENT", txn.Student?.UniversityID, txn.Student?.FullName,
                    $"Automatically paid fine of Rs. {txn.FineAmount} for overdue book '{txn.Book.Title}'");

                return (true, $"Book returned successfully. Overdue by {overdueDays} day(s) — Fine: Rs. {txn.FineAmount} (Paid)");
            }

            txn.Status = "Returned";
            txn.Book!.AvailableCopies++;
            await _db.SaveChangesAsync();

            await LogActivityAsync("BOOK_RETURN", txn.Student?.UniversityID, txn.Student?.FullName, $"Returned '{txn.Book.Title}' — On time");

            return (true, "Book returned successfully.");
        }

        public async Task<(bool, string)> ReturnBookByQRAsync(string returnQrCode, string universityId)
        {
            if (string.IsNullOrEmpty(returnQrCode) || !returnQrCode.StartsWith("RETURN-"))
                return (false, "Invalid return QR code.");

            var txnCode = returnQrCode.Substring("RETURN-".Length);

            var txn = await _db.Transactions
                .Include(t => t.Book)
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.TxnCode == txnCode);

            if (txn == null)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, null, "Return failed: Transaction not found");
                return (false, "Transaction not found.");
            }
            if (txn.Status == "Returned")
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, txn.Student?.FullName, "Return failed: Already returned");
                return (false, "Book already returned.");
            }
            if (txn.Student!.UniversityID != universityId)
            {
                await LogActivityAsync("FAILED_ATTEMPT", universityId, null, "Return failed: Not the borrower");
                return (false, "This isn't your book to return.");
            }

            txn.ReturnDate = DateTime.Now;

            if (txn.DueDate < DateTime.Now)
            {
                int overdueDays = (DateTime.Now - txn.DueDate).Days;
                txn.FineAmount = overdueDays * 20;
                txn.FinePaid = true;
                txn.Status = "Returned";
                txn.Book!.AvailableCopies++;
                await _db.SaveChangesAsync();

                await LogActivityAsync("BOOK_RETURN", universityId, txn.Student.FullName,
                    $"Returned '{txn.Book.Title}' via QR — Overdue {overdueDays} day(s), Fine: Rs. {txn.FineAmount}");

                await LogActivityAsync("FINE_PAYMENT", universityId, txn.Student.FullName,
                    $"Automatically paid fine of Rs. {txn.FineAmount} via QR for overdue book '{txn.Book.Title}'");

                return (true, $"Book returned! Overdue by {overdueDays} day(s) — Fine: Rs. {txn.FineAmount} (Paid)");
            }

            txn.Status = "Returned";
            txn.Book!.AvailableCopies++;
            await _db.SaveChangesAsync();

            await LogActivityAsync("BOOK_RETURN", universityId, txn.Student.FullName, $"Returned '{txn.Book.Title}' via QR — On time");

            return (true, "Book returned successfully.");
        }

        public async Task<(Book? Book, Transaction? ActiveTxn)> GetBookWithTxnAsync(string qrCode, string universityId)
        {
            var book = await _db.Books.FirstOrDefaultAsync(b => b.QRCode == qrCode);
            if (book == null) return (null, null);

            if (string.IsNullOrEmpty(universityId))
                return (book, null);

            var student = await _db.Students.FirstOrDefaultAsync(s => s.UniversityID == universityId);
            if (student == null) return (book, null);

            var activeTxn = await _db.Transactions
                .FirstOrDefaultAsync(t => t.BookID == book.BookID
                                        && t.StudentID == student.StudentID
                                        && (t.Status == "Active" || t.Status == "Overdue"));

            return (book, activeTxn);
        }

        public async Task<DashboardViewModel?> GetDashboardAsync(string universityId)
        {
            var student = await _db.Students.FirstOrDefaultAsync(s => s.UniversityID == universityId);
            if (student == null) return null;

            var allTxns = await _db.Transactions
                .Include(t => t.Book)
                .Where(t => t.StudentID == student.StudentID)
                .OrderByDescending(t => t.IssueDate)
                .ToListAsync();

            foreach (var t in allTxns.Where(t => t.Status == "Active" && t.DueDate < DateTime.Now))
                t.Status = "Overdue";
            await _db.SaveChangesAsync();

            return new DashboardViewModel
            {
                Student = student,
                ActiveBooks = allTxns.Where(t => t.Status is "Active" or "Overdue").ToList(),
                History = allTxns.Where(t => t.Status == "Returned").ToList()
            };
        }

        public async Task<int> GetActiveBorrowersCountAsync() =>
            await _db.Transactions
                .Where(t => t.Status == "Active" || t.Status == "Overdue")
                .Select(t => t.StudentID)
                .Distinct()
                .CountAsync();

        public async Task<List<Transaction>> GetTodayTransactionsAsync()
        {
            var today = DateTime.Today;
            return await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .Where(t => t.IssueDate.Date == today || (t.ReturnDate.HasValue && t.ReturnDate.Value.Date == today))
                .OrderByDescending(t => t.IssueDate)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetDefaultersAsync() =>
            await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .Where(t => (t.Status == "Overdue" || (t.Status == "Active" && t.DueDate < DateTime.Now))
                            || (t.FineAmount > 0 && !t.FinePaid))
                .OrderByDescending(t => t.DueDate)
                .ToListAsync();

        public async Task<(decimal TotalCalculated, decimal TotalPaid, decimal TotalOutstanding)> GetFineSummaryAsync()
        {
            var fineTxns = await _db.Transactions.Where(t => t.FineAmount > 0).ToListAsync();
            var total = fineTxns.Sum(t => t.FineAmount);
            var paid = fineTxns.Where(t => t.FinePaid).Sum(t => t.FineAmount);
            var outstanding = total - paid;
            return (total, paid, outstanding);
        }

        public async Task<List<Transaction>> GetFineDetailsAsync() =>
            await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .Where(t => t.FineAmount > 0)
                .OrderByDescending(t => t.IssueDate)
                .ToListAsync();

        public async Task<List<(string Title, string Author, int Count)>> GetMostBorrowedBooksAsync()
        {
            var data = await _db.Transactions
                .GroupBy(t => new { t.Book!.Title, t.Book.Author })
                .Select(g => new { g.Key.Title, g.Key.Author, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10)
                .ToListAsync();

            return data.Select(g => (g.Title, g.Author, g.Count)).ToList();
        }

        public async Task<List<Transaction>> GetCirculationLogAsync(DateTime? from, DateTime? to)
        {
            var query = _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(t => t.IssueDate.Date >= from.Value.Date);

            if (to.HasValue)
                query = query.Where(t => t.IssueDate.Date <= to.Value.Date);

            return await query.OrderByDescending(t => t.IssueDate).ToListAsync();
        }

        public async Task<(int TotalCopies, int AvailableCopies, int IssuedCopies)> GetInventoryStatusAsync()
        {
            var books = await _db.Books.ToListAsync();
            var total = books.Sum(b => b.TotalCopies);
            var available = books.Sum(b => b.AvailableCopies);
            return (total, available, total - available);
        }

        public async Task<(bool, string)> UpdateDueDateAsync(int transactionId, DateTime newDueDate)
        {
            var txn = await _db.Transactions.FindAsync(transactionId);
            if (txn == null) return (false, "Transaction not found.");
            if (txn.Status == "Returned") return (false, "Cannot edit due date of a returned book.");

            txn.DueDate = newDueDate;

            if (newDueDate >= DateTime.Now && txn.Status == "Overdue")
                txn.Status = "Active";
            else if (newDueDate < DateTime.Now && txn.Status == "Active")
                txn.Status = "Overdue";

            await _db.SaveChangesAsync();
            return (true, "Due date updated successfully.");
        }

        public async Task<List<Book>> GetAllBooksAsync() =>
            await _db.Books.OrderBy(b => b.Title).ToListAsync();

        public async Task<Book?> GetBookByQRAsync(string qrCode) =>
            await _db.Books.FirstOrDefaultAsync(b => b.QRCode == qrCode);

        public async Task<List<Transaction>> GetAllTransactionsAsync() =>
            await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .OrderByDescending(t => t.IssueDate)
                .ToListAsync();

        public Task<string> GenerateQRCodeBase64Async(string content)
        {
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"QR:{content}"));
            return Task.FromResult(base64);
        }

        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        private static string GenerateTxnCode()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var rand = new Random().Next(100000, 999999);
            return $"TXN-{datePart}-{rand}";
        }


        // ===================================================
        // 🧠 ML.NET TRUE DYNAMIC CORE RECOMMENDATION MATRIX ENGINE (FIXED)
        // ===================================================
        public async Task<List<Book>> GetAIRecommendationsAsync(string universityId)
        {
            try
            {
                var allBooks = await _db.Books.ToListAsync();

                // 🛠️ FIX 1: Eager load Student navigation property to access UniversityID
                var allTransactions = await _db.Transactions.Include(t => t.Student).ToListAsync();

                if (!allTransactions.Any() || !allBooks.Any())
                {
                    return allBooks.Take(3).ToList();
                }

                // 1. Structural ML Dataset Parsing Configuration
                var trainingData = allTransactions
                    .Where(t => t.Student != null) // Safe check
                    .Select(t => new BookBorrowEvent
                    {
                        // 🛠️ FIX 2: Use t.Student.UniversityID instead of t.UniversityID
                        StudentKey = (uint)Math.Abs(t.Student!.UniversityID.GetHashCode() % 10000),
                        BookKey = (uint)Math.Abs(t.BookID.GetHashCode() % 10000),
                        Label = 1.0f
                    }).ToList();

                var mlContext = new MLContext();
                var trainDataView = mlContext.Data.LoadFromEnumerable(trainingData);

                // 2. Machine Learning Collaborative Filtering Pipelines Setup
                var options = new MatrixFactorizationTrainer.Options
                {
                    MatrixColumnIndexColumnName = nameof(BookBorrowEvent.StudentKey),
                    MatrixRowIndexColumnName = nameof(BookBorrowEvent.BookKey),
                    LabelColumnName = nameof(BookBorrowEvent.Label),
                    NumberOfIterations = 20,
                    ApproximationRank = 32
                };

                var pipeline = mlContext.Recommendation().Trainers.MatrixFactorization(options);
                var model = pipeline.Fit(trainDataView);
                var predictionEngine = mlContext.Model.CreatePredictionEngine<BookBorrowEvent, BookPrediction>(model);

                // 3. User Specific Isolation Metrics
                var currentStudentHistory = allTransactions
                    .Where(t => t.Student != null && t.Student.UniversityID == universityId) // 🛠️ FIX 3: Updated here as well
                    .Select(t => t.BookID)
                    .ToList();

                var unreadBooks = allBooks.Where(b => !currentStudentHistory.Contains(b.BookID)).ToList();
                var targetStudentKey = (uint)Math.Abs(universityId.GetHashCode() % 10000);

                // 4. Compute Predictive Match Scores
                var scoredRecommendations = unreadBooks.Select(book => new
                {
                    Book = book,
                    Prediction = predictionEngine.Predict(new BookBorrowEvent
                    {
                        StudentKey = targetStudentKey,
                        BookKey = (uint)Math.Abs(book.BookID.GetHashCode() % 10000)
                    })
                })
                .OrderByDescending(r => r.Prediction.Score)
                .Take(3)
                .Select(r => r.Book)
                .ToList();

                if (!scoredRecommendations.Any())
                {
                    return allBooks.Take(3).ToList();
                }

                return scoredRecommendations;
            }
            catch
            {
                return await _db.Books.Take(3).ToListAsync();
            }
        }


        // Activity Logging
        // ──────────────────────────────────────────────
        public async Task LogActivityAsync(string actionType, string? userId, string? userName, string? details, string? ipAddress = null)
        {
            var log = new ActivityLog
            {
                ActionType = actionType,
                UserID = userId,
                UserName = userName,
                Details = details,
                IPAddress = ipAddress,
                Timestamp = DateTime.Now
            };
            _db.ActivityLogs.Add(log);
            await _db.SaveChangesAsync();
        }


        public async Task<List<ActivityLog>> GetActivityLogsAsync(string? actionType = null, int take = 100)
        {
            var query = _db.ActivityLogs.AsQueryable();
            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(l => l.ActionType == actionType);

            return await query
                .OrderByDescending(l => l.Timestamp)
                .Take(take)
                .ToListAsync();
        }

        // ──────────────────────────────────────────────
        // Analytics Dashboard
        // ──────────────────────────────────────────────

        public async Task<List<(string FullName, string UniversityID, int BorrowCount)>> GetMostActiveStudentsAsync()
        {
            var data = await _db.Transactions
                .GroupBy(t => new { t.Student!.FullName, t.Student.UniversityID })
                .Select(g => new { g.Key.FullName, g.Key.UniversityID, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10)
                .ToListAsync();

            return data.Select(g => (g.FullName, g.UniversityID, g.Count)).ToList();
        }

        public async Task<List<(int Hour, int Count)>> GetPeakIssuingTimingsAsync()
        {
            var txns = await _db.Transactions.Select(t => t.IssueDate.Hour).ToListAsync();
            return txns
                .GroupBy(h => h)
                .Select(g => (g.Key, g.Count()))
                .OrderBy(g => g.Item1)
                .ToList();
        }

        public async Task<List<(string Month, decimal TotalFine)>> GetFineTrendsAsync()
        {
            var txns = await _db.Transactions
                .Where(t => t.FineAmount > 0)
                .Select(t => new { t.IssueDate, t.FineAmount })
                .ToListAsync();

            return txns
                .GroupBy(t => t.IssueDate.ToString("MMM yyyy"))
                .Select(g => (g.Key, g.Sum(x => x.FineAmount)))
                .OrderBy(g => DateTime.Parse("01 " + g.Item1))
                .ToList();
        }

        public async Task<List<(string Batch, int StudentCount, int TotalBorrows)>> GetBatchWiseStatsAsync()
        {
            var students = await _db.Students
                .Select(s => new { s.StudentID, s.Batch })
                .ToListAsync();

            var txnCounts = await _db.Transactions
                .GroupBy(t => t.StudentID)
                .Select(g => new { StudentID = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = students
                .GroupBy(s => s.Batch)
                .Select(g => (
                    g.Key,
                    g.Count(),
                    g.Sum(s => txnCounts.FirstOrDefault(t => t.StudentID == s.StudentID)?.Count ?? 0)
                ))
                .OrderBy(g => g.Item1)
                .ToList();

            return result;
        }

        // 📦 QUICK STOCK UPDATE TRANSACTION ENGINE METHOD
        public async Task<(bool Success, string Message)> AddBookCopiesAsync(int bookId, int additionalCopies)
        {
            if (additionalCopies <= 0)
                return (false, "Please enter a valid number of copies.");

            var book = await _db.Books.FindAsync(bookId);
            if (book == null)
                return (false, "Book not found.");

            // Increment database tracking inventory arrays concurrently
            book.TotalCopies += additionalCopies;
            book.AvailableCopies += additionalCopies;

            await _db.SaveChangesAsync();

            // Track state via standard internal activity auditing log
            await LogActivityAsync("INVENTORY_UPDATE", null, "Admin", $"Added {additionalCopies} copies to '{book.Title}' (ID: {bookId})");

            return (true, $"Successfully added {additionalCopies} copies to '{book.Title}'.");
        }
    }
}