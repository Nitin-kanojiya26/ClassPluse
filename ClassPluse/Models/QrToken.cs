using System.ComponentModel.DataAnnotations;

namespace ClassPluse.Models
{
    public class QrToken
    {
        [Key]
        public int Id { get; set; }

        public int LectureSessionId { get; set; }
        public LectureSession? LectureSession { get; set; }

        [Required]
        public string EncryptedPayload { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
