using System.ComponentModel.DataAnnotations;

namespace ClassPluse.Models
{
    public class Course
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string CourseCode { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string CourseName { get; set; } = string.Empty;

        public string FacultyId { get; set; } = string.Empty;

        // Navigation properties
        public ApplicationUser? Faculty { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<LectureSession> LectureSessions { get; set; } = new List<LectureSession>();
    }
}
