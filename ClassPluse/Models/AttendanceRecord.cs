using System.ComponentModel.DataAnnotations;

namespace ClassPluse.Models
{
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Excused
    }

    public enum MarkedByType
    {
        Scan,
        ManualFaculty
    }

    public class AttendanceRecord
    {
        [Key]
        public int Id { get; set; }

        public int LectureSessionId { get; set; }
        public LectureSession? LectureSession { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public AttendanceStatus Status { get; set; }

        public DateTime MarkedAt { get; set; }

        public MarkedByType MarkedBy { get; set; }

        [StringLength(50)]
        public string? IPAddress { get; set; }
    }
}
