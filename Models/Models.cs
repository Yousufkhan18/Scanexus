using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.ML.Data;

namespace LibrarySystem.Models
{
    public class Student
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StudentID { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "University ID")]
        public string UniversityID { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "Father Name")]
        public string FatherName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Range(1, 8)]
        public int Semester { get; set; }

        [StringLength(20)]
        public string Batch { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        public string? Department { get; set; }
    }

    public class Admin
    {
        [Key]
        [StringLength(20)]
        public string AdminID { get; set; } = string.Empty;

        [Required, StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Email { get; set; }
    }

    public class Book
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookID { get; set; }

        [Required, StringLength(20)]
        public string ISBN { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Author { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Publisher { get; set; }

        public int TotalCopies { get; set; } = 1;
        public int AvailableCopies { get; set; } = 1;

        [Required, StringLength(500)]
        public string QRCode { get; set; } = string.Empty;

        public DateTime AddedAt { get; set; } = DateTime.Now;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }

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

    public class Transaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TransactionID { get; set; }

        [Required, StringLength(50)]
        public string TxnCode { get; set; } = string.Empty;

        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public Student? Student { get; set; }

        [ForeignKey("Book")]
        public int BookID { get; set; }
        public Book? Book { get; set; }

        public DateTime IssueDate { get; set; } = DateTime.Now;
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        [Required, StringLength(500)]
        public string QRScanData { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Remarks { get; set; }

        [NotMapped]
        public bool IsOverdue => (Status == "Active" || Status == "Overdue") && DueDate < DateTime.Now;

        public decimal FineAmount { get; set; } = 0;
        public bool FinePaid { get; set; } = false;

        [NotMapped]
        public int OverdueDays
        {
            get
            {
                if (ReturnDate.HasValue)
                {
                    int days = (ReturnDate.Value - DueDate).Days;
                    return days > 0 ? days : 0;
                }
                else if (DueDate < DateTime.Now)
                {
                    int days = (DateTime.Now - DueDate).Days;
                    return days > 0 ? days : 0;
                }
                return 0;
            }
        }

        [NotMapped]
        public decimal CalculatedFine
        {
            get
            {
                if (Status == "Returned")
                {
                    return 0;
                }
                return OverdueDays * 20;
            }
        }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "University ID is required")]
        [Display(Name = "University ID")]
        public string UniversityID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class DashboardViewModel
    {
        public Student Student { get; set; } = new Student();
        public List<Transaction> ActiveBooks { get; set; } = new();
        public List<Transaction> History { get; set; } = new();
        public int BooksRemaining => 3 - ActiveBooks.Count;
        public List<Book> AIRecommendations { get; set; } = new List<Book>();
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class IssueBookRequest
    {
        [Required] public string UniversityID { get; set; } = string.Empty;
        [Required] public string QRCode { get; set; } = string.Empty;
    }

    public class ReturnBookRequest
    {
        [Required] public string TxnCode { get; set; } = string.Empty;
    }

    public class QRScanViewModel
    {
        [Required]
        public string QRCode { get; set; } = string.Empty;
    }

    public class ReturnByQRRequest
    {
        [Required] public string ReturnQrCode { get; set; } = string.Empty;
        [Required] public string UniversityId { get; set; } = string.Empty;
    }

    public class ActivityLog
    {
        [Key]
        public int LogID { get; set; }

        [Required, StringLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [StringLength(20)]
        public string? UserID { get; set; }

        [StringLength(100)]
        public string? UserName { get; set; }

        [StringLength(500)]
        public string? Details { get; set; }

        [StringLength(50)]
        public string? IPAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}