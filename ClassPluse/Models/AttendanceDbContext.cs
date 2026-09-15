using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClassPluse.Models
{
    public class AttendanceDbContext : IdentityDbContext<ApplicationUser>
    {
        public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options)
            : base(options)
        {
        }

        public DbSet<Course> Courses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<LectureSession> LectureSessions { get; set; }
        public DbSet<QrToken> QrTokens { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure unique constraint on AttendanceRecord
            builder.Entity<AttendanceRecord>()
                .HasIndex(a => new { a.LectureSessionId, a.StudentId })
                .IsUnique();

            // Configure relationships
            builder.Entity<Enrollment>()
                .HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Enrollment>()
                .HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LectureSession>()
                .HasOne(ls => ls.Course)
                .WithMany(c => c.LectureSessions)
                .HasForeignKey(ls => ls.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LectureSession>()
                .HasOne(ls => ls.Faculty)
                .WithMany()
                .HasForeignKey(ls => ls.FacultyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.LectureSession)
                .WithMany(ls => ls.AttendanceRecords)
                .HasForeignKey(ar => ar.LectureSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.Student)
                .WithMany()
                .HasForeignKey(ar => ar.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.Entity<QrToken>()
                .HasOne(qt => qt.LectureSession)
                .WithMany(ls => ls.QrTokens)
                .HasForeignKey(qt => qt.LectureSessionId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<Course>()
                .HasOne(c => c.Faculty)
                .WithMany()
                .HasForeignKey(c => c.FacultyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
