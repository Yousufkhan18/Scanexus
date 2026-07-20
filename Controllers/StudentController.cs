using LibrarySystem.Models;
using LibrarySystem.Services;
using Microsoft.AspNetCore.Mvc;
using LibrarySystem.Data;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Controllers
{
    public class StudentController : Controller
    {
        private readonly ILibraryService _service;
        private readonly LibraryDbContext _db;

        public StudentController(ILibraryService service, LibraryDbContext db)
        {
            _service = service;
            _db = db;
        }

        public IActionResult Login() => View();
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

          
            if (model.UniversityID == "ADMIN-001" && model.Password == "Admin@123")
            {
                HttpContext.Session.SetString("UniversityID", "ADMIN-001");
                HttpContext.Session.SetString("StudentName", "Library Admin");
                HttpContext.Session.SetString("Role", "Admin");

                await _service.LogActivityAsync("LOGIN", "ADMIN-001", "Library Admin", "Admin login successful", ipAddress);

                return RedirectToAction("Dashboard", "Admin");
            }

            var hashedPassword = LibraryService.HashPassword(model.Password);

            var student = await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UniversityID == model.UniversityID && s.PasswordHash == hashedPassword);

            if (student != null)
            {
                if (!student.IsActive)
                {
                    await _service.LogActivityAsync("FAILED_ATTEMPT", model.UniversityID, student.FullName, "Login failed: Inactive account", ipAddress);
                    TempData["Error"] = "Your account is inactive. Contact the library.";
                    return View(model);
                }

                HttpContext.Session.SetString("UniversityID", student.UniversityID);
                HttpContext.Session.SetString("StudentID", student.StudentID.ToString());
                HttpContext.Session.SetString("StudentName", student.FullName);
                HttpContext.Session.SetString("Role", "Student");

                await _service.LogActivityAsync("LOGIN", student.UniversityID, student.FullName, "Student login successful", ipAddress);

                return RedirectToAction("Dashboard");
            }

           
            await _service.LogActivityAsync("FAILED_ATTEMPT", model.UniversityID, null, "Login failed: Invalid credentials", ipAddress);
            TempData["Error"] = "Invalid credentials.";
            return View(model);
        }

        public async Task<IActionResult> Dashboard()
        {
            var uid = HttpContext.Session.GetString("UniversityID");
            if (string.IsNullOrEmpty(uid)) return RedirectToAction("Login");

            var vm = await _service.GetDashboardAsync(uid);
            if (vm == null) return RedirectToAction("Login");

            var overdueBooksList = vm.ActiveBooks != null
                ? vm.ActiveBooks.Where(t => t.IsOverdue).ToList()
                : new List<LibrarySystem.Models.Transaction>();

            ViewBag.OverdueNotifications = overdueBooksList.Select(t => new {
                Message = "Book '" + (t.Book != null ? t.Book.Title : "Library Book") + "' is Overdue! It was due on " + t.DueDate.ToString("dd MMM yyyy") + ". Please return it to stop fine accumulation."
            }).ToList();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(string txnCode)
        {
            var uid = HttpContext.Session.GetString("UniversityID");
            if (string.IsNullOrEmpty(uid)) return RedirectToAction("Login");

            var (success, message) = await _service.ReturnBookAsync(txnCode);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction("Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}