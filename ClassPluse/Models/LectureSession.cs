using System.ComponentModel.DataAnnotations;

namespace ClassPluse.Models
{
    public class LectureSession
    {
        [Key]
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public string FacultyId { get; set; } = string.Empty;
        public ApplicationUser? Faculty { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<QrToken> QrTokens { get; set; } = new List<QrToken>();
        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    }
}
