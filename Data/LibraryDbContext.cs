using Microsoft.EntityFrameworkCore;
using LibrarySystem.Models;

namespace LibrarySystem.Data
{
    public class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options) { }

        public DbSet<Student>     Students     { get; set; }
        public DbSet<Book>        Books        { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        public DbSet<Admin> Admins { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Student>()
              .HasIndex(s => s.UniversityID).IsUnique();
            mb.Entity<Student>()
              .HasIndex(s => s.Email).IsUnique();

            
            mb.Entity<Book>()
              .HasIndex(b => b.ISBN).IsUnique();
            mb.Entity<Book>()
              .HasIndex(b => b.QRCode).IsUnique();

            mb.Entity<Transaction>()
              .HasIndex(t => t.TxnCode).IsUnique();

            mb.Entity<Transaction>()
              .HasOne(t => t.Student)
              .WithMany(s => s.Transactions)
              .HasForeignKey(t => t.StudentID)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Transaction>()
              .HasOne(t => t.Book)
              .WithMany(b => b.Transactions)
              .HasForeignKey(t => t.BookID)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Book>()
              .Property(b => b.AvailableCopies)
              .HasDefaultValue(1);
        }
    }
}
