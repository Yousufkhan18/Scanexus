using LibrarySystem.Services;
using LibrarySystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace LibrarySystem.Controllers
{
    public class BooksController : Controller
    {
        private readonly ILibraryService _service;
        public BooksController(ILibraryService service) => _service = service;

        public async Task<IActionResult> Index()
        {
            var uid = HttpContext.Session.GetString("UniversityID");
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Login", "Student");

            var books = await _service.GetAllBooksAsync();

            // 🧠 Dynamic AI trigger linked seamlessly to your ML.NET service implementation
            ViewBag.AIRecommendations = await _service.GetAIRecommendationsAsync(uid);

            return View(books);
        }

        public async Task<IActionResult> Borrow(string qr)
        {
            var book = await _service.GetBookByQRAsync(qr);
            if (book == null) return NotFound("Book not found.");

            ViewBag.BookTitle = book.Title;
            ViewBag.BookAuthor = book.Author;
            ViewBag.QRCode = book.QRCode;
            ViewBag.Available = book.AvailableCopies > 0;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(string qrCode)
        {
            var uid = HttpContext.Session.GetString("UniversityID");
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Login", "Student");

            var (success, message, txn) = await _service.IssueBookAsync(uid, qrCode);

            if (success)
            {
                TempData["Receipt"] = System.Text.Json.JsonSerializer.Serialize(new
                {
                    txn!.TxnCode,
                    BookTitle = txn.Book?.Title,
                    BookAuthor = txn.Book?.Author,
                    StudentName = txn.Student?.FullName,
                    UniversityID = uid,
                    IssueDate = txn.IssueDate.ToString("dd MMM yyyy, hh:mm tt"),
                    DueDate = txn.DueDate.ToString("dd MMM yyyy")
                });
            }
            else
            {
                TempData["Error"] = message;
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Detail(string qr)
        {
            var uid = HttpContext.Session.GetString("UniversityID") ?? "";
            var (book, activeTxn) = await _service.GetBookWithTxnAsync(qr, uid);

            if (book == null) return NotFound();

            return Json(new
            {
                available = book.AvailableCopies > 0,
                hasActiveTxn = activeTxn != null,
                borrowQR = book.QRCode,
                returnQR = activeTxn != null ? "RETURN-" + activeTxn.TxnCode : null,
                dueDate = activeTxn != null ? activeTxn.DueDate.ToString("dd MMM yyyy") : null
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnByQR(string returnQrCode)
        {
            var uid = HttpContext.Session.GetString("UniversityID") ?? "";
            if (string.IsNullOrEmpty(uid))
            {
                TempData["Error"] = "Please login first.";
                return RedirectToAction("Index");
            }

            var (success, message) = await _service.ReturnBookByQRAsync(returnQrCode, uid);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction("Index");
        }
    }
}