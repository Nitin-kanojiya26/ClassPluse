using System.ComponentModel.DataAnnotations;

namespace ClassPluse.Models
{
    public class Enrollment
    {
        [Key]
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }
    }
}
