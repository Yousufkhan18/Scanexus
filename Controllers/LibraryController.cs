using LibrarySystem.Models;
using LibrarySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LibrarySystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : ControllerBase
    {
        private readonly ILibraryService _service;

        public LibraryController(ILibraryService service) => _service = service;


        [HttpPost("token")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> GenerateToken([FromBody] TokenLoginRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid request payload." });
            var (success, message, student) = await _service.AuthenticateAsync(model.UniversityID, model.Password);

            if (!success || student == null)
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "API Identity verification failed: " + message });

      
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, student.UniversityID),
                new Claim(ClaimTypes.Role, student.UniversityID.ToLower().Contains("admin") ? "Admin" : "Student"),
                new Claim("FullName", student.FullName ?? "Academic Entity"),
                new Claim("Semester", student.Semester.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Scanexus_Secure_Enterprise_Secret_Key_2026_JWT"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "ScanexusEngine",
                audience: "ScanexusStudents",
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: creds
            );

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "JWT Bearer Token issued successfully.",
                Data = new
                {
                    Token = new JwtSecurityTokenHandler().WriteToken(token),
                    Expiration = token.ValidTo,
                    Profile = new { student.UniversityID, student.FullName }
                }
            });
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid request." });

            var (success, message, student) = await _service.AuthenticateAsync(model.UniversityID, model.Password);

            if (!success)
                return Unauthorized(new ApiResponse<object> { Success = false, Message = message });

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = message,
                Data = new
                {
                    student!.UniversityID,
                    student.FullName,
                    student.Semester,
                    student.Batch,
                    student.IsActive
                }
            });
        }

        [HttpPost("issue")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> IssueBook([FromBody] IssueBookRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid request." });

            var (success, message, txn) = await _service.IssueBookAsync(request.UniversityID, request.QRCode);

            if (!success)
                return BadRequest(new ApiResponse<object> { Success = false, Message = message });

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = message,
                Data = new
                {
                    txn!.TxnCode,
                    txn.IssueDate,
                    txn.DueDate,
                    BookTitle = txn.Book?.Title,
                    BookAuthor = txn.Book?.Author,
                    StudentName = txn.Student?.FullName
                }
            });
        }

        [HttpPost("return")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> ReturnBook([FromBody] ReturnBookRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid request." });

            var (success, message) = await _service.ReturnBookAsync(request.TxnCode);

            if (!success)
                return BadRequest(new ApiResponse<object> { Success = false, Message = message });

            return Ok(new ApiResponse<object> { Success = true, Message = message });
        }

        [HttpGet("books")]
        [ProducesResponseType(typeof(ApiResponse<List<object>>), 200)]
        public async Task<IActionResult> GetBooks()
        {
            var books = await _service.GetAllBooksAsync();
            var data = books.Select(b => new
            {
                b.BookID,
                b.ISBN,
                b.Title,
                b.Author,
                b.Publisher,
                b.TotalCopies,
                b.AvailableCopies,
                b.QRCode,
                IsAvailable = b.AvailableCopies > 0
            }).ToList();

            return Ok(new ApiResponse<object> { Success = true, Message = "OK", Data = data });
        }

        [HttpGet("dashboard/{universityId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> GetDashboard(string universityId)
        {
            var dashboard = await _service.GetDashboardAsync(universityId);
            if (dashboard == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Student not found." });

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "OK",
                Data = new
                {
                    dashboard.Student.FullName,
                    dashboard.Student.UniversityID,
                    dashboard.Student.Semester,
                    BooksRemaining = dashboard.BooksRemaining,
                    ActiveBooks = dashboard.ActiveBooks.Select(t => new
                    {
                        t.TxnCode,
                        BookTitle = t.Book?.Title,
                        BookAuthor = t.Book?.Author,
                        t.IssueDate,
                        t.DueDate,
                        t.Status,
                        IsOverdue = t.IsOverdue
                    }),
                    History = dashboard.History.Select(t => new
                    {
                        t.TxnCode,
                        BookTitle = t.Book?.Title,
                        t.IssueDate,
                        t.ReturnDate,
                        t.Status
                    })
                }
            });
        }

        [HttpGet("scan")]
        public async Task<IActionResult> ScanQR([FromQuery] string qr)
        {
            if (string.IsNullOrWhiteSpace(qr))
                return BadRequest(new ApiResponse<object> { Success = false, Message = "QR code required." });

            var book = await _service.GetBookByQRAsync(qr);
            if (book == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Book not found." });

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Book found.",
                Data = new
                {
                    book.BookID,
                    book.Title,
                    book.Author,
                    book.ISBN,
                    book.Publisher,
                    book.AvailableCopies,
                    IsAvailable = book.AvailableCopies > 0
                }
            });
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            var txns = await _service.GetAllTransactionsAsync();
            var data = txns.Select(t => new
            {
                t.TxnCode,
                t.Status,
                StudentID = t.Student?.UniversityID,
                StudentName = t.Student?.FullName,
                BookTitle = t.Book?.Title,
                t.IssueDate,
                t.DueDate,
                t.ReturnDate
            });

            return Ok(new ApiResponse<object> { Success = true, Message = "OK", Data = data });
        }

        [HttpPost("return-by-qr")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> ReturnByQR([FromBody] ReturnByQRRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid request." });

            var (success, message) = await _service.ReturnBookByQRAsync(request.ReturnQrCode, request.UniversityId);

            if (!success)
                return BadRequest(new ApiResponse<object> { Success = false, Message = message });

            return Ok(new ApiResponse<object> { Success = true, Message = message });
        }

        [HttpGet("defaulters")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> GetDefaulters()
        {
            var defaulters = await _service.GetDefaultersAsync();
            var data = defaulters.Select(t => new
            {
                t.TxnCode,
                StudentName = t.Student?.FullName,
                StudentID = t.Student?.UniversityID,
                BookTitle = t.Book?.Title,
                t.DueDate,
                Fine = t.FineAmount > 0 ? t.FineAmount : t.CalculatedFine,
                t.Status
            });

            return Ok(new ApiResponse<object> { Success = true, Message = "OK", Data = data });
        }

        [HttpGet("fine-summary")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> GetFineSummary()
        {
            var summary = await _service.GetFineSummaryAsync();
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "OK",
                Data = new
                {
                    TotalCalculated = summary.TotalCalculated,
                    TotalPaid = summary.TotalPaid,
                    TotalOutstanding = summary.TotalOutstanding
                }
            });
        }

        [HttpGet("most-borrowed")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> GetMostBorrowed()
        {
            var books = await _service.GetMostBorrowedBooksAsync();
            var data = books.Select(b => new { b.Title, b.Author, b.Count });
            return Ok(new ApiResponse<object> { Success = true, Message = "OK", Data = data });
        }

        [HttpGet("today-transactions")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> GetTodayTransactions()
        {
            var txns = await _service.GetTodayTransactionsAsync();
            var data = txns.Select(t => new
            {
                t.TxnCode,
                StudentName = t.Student?.FullName,
                BookTitle = t.Book?.Title,
                t.IssueDate,
                t.ReturnDate,
                t.Status
            });

            return Ok(new ApiResponse<object> { Success = true, Message = "OK", Data = data });
        }

        [HttpGet("inventory-status")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> GetInventoryStatus()
        {
            var inventory = await _service.GetInventoryStatusAsync();
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "OK",
                Data = new
                {
                    TotalCopies = inventory.TotalCopies,
                    AvailableCopies = inventory.AvailableCopies,
                    IssuedCopies = inventory.IssuedCopies
                }
            });
        }
    }

   
    public class TokenLoginRequest
    {
        public string UniversityID { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}