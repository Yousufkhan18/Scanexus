using LibrarySystem.Data;
using LibrarySystem.Models;
using LibrarySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Controllers
{
    public class AdminController : Controller
    {
        private readonly ILibraryService _service;
        private readonly LibraryDbContext _db;

        public AdminController(ILibraryService service, LibraryDbContext db)
        {
            _service = service;
            _db = db;
        }

        private bool IsAdmin() => HttpContext.Session.GetString("Role") == "Admin";

        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");

            ViewBag.TotalBooks = await _db.Books.CountAsync();
            ViewBag.TotalStudents = await _db.Students.CountAsync();
            ViewBag.ActiveIssues = await _db.Transactions.CountAsync(t => t.Status == "Active" || t.Status == "Overdue");
            ViewBag.OverdueIssues = await _db.Transactions.CountAsync(t => t.Status == "Overdue" || (t.Status == "Active" && t.DueDate < DateTime.Now));
            ViewBag.TotalTransactions = await _db.Transactions.CountAsync();

            ViewBag.TotalCopies = await _db.Books.SumAsync(b => b.TotalCopies);
            ViewBag.AvailableCopies = await _db.Books.SumAsync(b => b.AvailableCopies);
            ViewBag.IssuedCopies = ViewBag.TotalCopies - ViewBag.AvailableCopies;

            ViewBag.TodayTxns = await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .Where(t => t.IssueDate.Date == DateTime.Today || (t.ReturnDate.HasValue && t.ReturnDate.Value.Date == DateTime.Today))
                .ToListAsync();

            ViewBag.Defaulters = await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .Where(t => t.Status == "Overdue" || (t.Status == "Active" && t.DueDate < DateTime.Now))
                .ToListAsync();

            var recentTxns = await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .OrderByDescending((Transaction t) => t.IssueDate)
                .ToListAsync();

            return View(recentTxns);
        }

        public async Task<IActionResult> Books()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");
            var books = await _db.Books.OrderBy(b => b.Title).ToListAsync();
            return View("AdminBooks", books);
        }

        public IActionResult AddBook()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");
            return View("AdminAddBook");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBook(Book book)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");

            var existing = await _db.Books.FirstOrDefaultAsync(b =>
                b.Title.ToLower() == book.Title.ToLower() &&
                b.Author.ToLower() == book.Author.ToLower());

            if (existing != null)
            {
                existing.TotalCopies += book.TotalCopies;
                existing.AvailableCopies += book.TotalCopies;
                await _db.SaveChangesAsync();
                TempData["Success"] = $"'{existing.Title}' already exists — {book.TotalCopies} copies added!";
                return RedirectToAction("Books");
            }

            var lastBook = await _db.Books
                .OrderByDescending((Book b) => b.BookID)
                .FirstOrDefaultAsync();

            int nextNum = (lastBook != null) ? lastBook.BookID + 1 : 1;
            book.QRCode = "SSUET-LIB-BOOK-" + nextNum.ToString("D3");
            book.ISBN = $"978-0-13-{nextNum:D4}";
            book.AvailableCopies = book.TotalCopies;
            book.AddedAt = DateTime.Now;

            _db.Books.Add(book);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Book '{book.Title}' added successfully!";
            return RedirectToAction("Books");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCopies(int bookId, int additionalCopies)
        {
            var (success, message) = await _service.AddBookCopiesAsync(bookId, additionalCopies);

            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction("Books"); 
        }

        public async Task<IActionResult> LastBookISBN()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");
            var lastBook = await _db.Books
                .OrderByDescending((Book b) => b.BookID)
                .FirstOrDefaultAsync();

            int nextNum = (lastBook != null) ? lastBook.BookID + 1 : 1;
            string isbn = $"978-0-13-{nextNum:D4}";
            string qr = "SSUET-LIB-BOOK-" + nextNum.ToString("D3");
            return Json(new { isbn, qr });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBook(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");

            var hasActive = await _db.Transactions.AnyAsync(t => t.BookID == id && t.Status == "Active");
            if (hasActive)
            {
                TempData["Error"] = "Cannot delete — this book has active issues.";
                return RedirectToAction("Books");
            }

            var relatedTxns = await _db.Transactions.Where(t => t.BookID == id).ToListAsync();
            if (relatedTxns.Any())
                _db.Transactions.RemoveRange(relatedTxns);

            var book = await _db.Books.FindAsync(id);
            if (book != null)
                _db.Books.Remove(book);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Book deleted successfully.";
            return RedirectToAction("Books");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDueDate(int transactionId, DateTime newDueDate)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");

            var txn = await _db.Transactions.FindAsync(transactionId);
            if (txn != null)
            {
                txn.DueDate = newDueDate;
                await _db.SaveChangesAsync();
                TempData["Success"] = "Due date updated successfully.";
            }
            else
            {
                TempData["Error"] = "Transaction not found.";
            }
            return RedirectToAction("Transactions");
        }

        public async Task<IActionResult> Students()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");
            var students = await _db.Students
                .OrderBy(s => s.FullName)
                .ToListAsync();
            return View("AdminStudents", students);
        }

        public IActionResult AddStudent()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");
            return View("AdminAddStudent");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(Student student)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");

            var existing = await _db.Students.FirstOrDefaultAsync(s =>
                s.UniversityID == student.UniversityID);

            if (existing != null)
            {
                TempData["Error"] = $"Student '{student.UniversityID}' already exists!";
                return View(student);
            }

            if (!string.IsNullOrEmpty(student.PasswordHash))
            {
                student.PasswordHash = LibraryService.HashPassword(student.PasswordHash);
            }

            student.IsActive = true;
            student.CreatedAt = DateTime.Now;

            _db.Students.Add(student);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Student '{student.FullName}' added successfully!";
            return RedirectToAction("Students");
        }

        public async Task<IActionResult> Transactions()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");
            var txns = await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .OrderByDescending((Transaction t) => t.IssueDate)
                .ToListAsync();
            return View("AdminTransactions", txns);
        }

        public async Task<IActionResult> Reports()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");

            var mostBorrowed = await _db.Transactions
                .Include(t => t.Book)
                .Where(t => t.Book != null)
                .GroupBy(t => new { t.BookID, t.Book!.Title, t.Book.Author })
                .Select(g => new { Title = g.Key.Title, Author = g.Key.Author, BorrowCount = g.Count() })
                .OrderByDescending(x => x.BorrowCount)
                .Take(5)
                .ToListAsync();

            var circulationLog = await _db.Transactions
                .Include(t => t.Student)
                .Include(t => t.Book)
                .OrderByDescending(t => t.IssueDate)
                .ToListAsync();

            ViewBag.MostBorrowed = mostBorrowed;
            ViewBag.CirculationLog = circulationLog;
            ViewBag.TotalIssued = await _db.Transactions.CountAsync(t => t.Status == "Active");
            ViewBag.TotalReturned = await _db.Transactions.CountAsync(t => t.Status == "Returned");
            ViewBag.TotalOverdue = await _db.Transactions.CountAsync(t => t.Status == "Overdue" || (t.Status == "Active" && t.DueDate < DateTime.Now));

            decimal historyPaidTotal = await _db.Transactions
                .Where(t => t.Status == "Returned" && t.FineAmount > 0)
                .SumAsync(t => t.FineAmount);

            decimal currentActiveTotal = circulationLog
                .Where(t => t.Status == "Overdue" || (t.Status == "Active" && t.DueDate < DateTime.Now))
                .Sum(t => t.CalculatedFine);

            decimal realTotalCalculated = historyPaidTotal + currentActiveTotal;

            ViewBag.TotalFineCalculated = realTotalCalculated;
            ViewBag.TotalFinePaid = historyPaidTotal;
            ViewBag.TotalFineOutstanding = realTotalCalculated - historyPaidTotal;

            ViewBag.FineDetails = circulationLog
                .Where(t => t.FineAmount > 0 || t.Status == "Overdue" || (t.Status == "Active" && t.DueDate < DateTime.Now))
                .ToList();

            return View("AdminReports");
        }

  
        public async Task<IActionResult> ActivityLogs(string actionType = null)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Login", "Student");

            var logs = await _service.GetActivityLogsAsync(actionType, take: 200);
            return View(logs);
        }


        public async Task<IActionResult> Analytics()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");

            ViewBag.MostActiveStudents = await _service.GetMostActiveStudentsAsync();
            ViewBag.PopularBooks = await _service.GetMostBorrowedBooksAsync();
            ViewBag.PeakTimings = await _service.GetPeakIssuingTimingsAsync();
            ViewBag.FineTrends = await _service.GetFineTrendsAsync();

            ViewBag.DepartmentStats = await _db.Transactions
                .Include(t => t.Student)
                .Where(t => t.Student != null)
                .GroupBy(t => new { t.Student!.Department, t.Student!.Batch })
                .Select(g => new {
                    Department = g.Key.Department ?? "Computer Science",
                    Batch = g.Key.Batch ?? "General Scope",
                    TotalStudents = _db.Students.Count(s => s.Department == g.Key.Department && s.Batch == g.Key.Batch),
                    TotalBorrows = g.Count(),
                    AvgBorrows = Math.Round((double)g.Count() / (_db.Students.Count(s => s.Department == g.Key.Department && s.Batch == g.Key.Batch) == 0 ? 1 : _db.Students.Count(s => s.Department == g.Key.Department && s.Batch == g.Key.Batch)), 1)
                })
                .OrderBy(x => x.Department)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStudent(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Student");
            var student = await _db.Students.FindAsync(id);
            if (student != null)
            {
                student.IsActive = !student.IsActive;
                await _db.SaveChangesAsync();
                TempData["Success"] = $"{student.FullName} is now {(student.IsActive ? "Active" : "Inactive")}.";
            }
            return RedirectToAction("Students");
        }
    }
}