using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ClassPluse.Models
{
    /// <summary>
    /// Represents a user in the application (Admin, Faculty, Student).
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? RollNumber { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(100)]
        [EmailAddress]
        public string? PersonalEmail { get; set; }

        // Face Recognition Properties
        public bool HasRegisteredFace { get; set; } = false;

        /// <summary>
        /// Stores the 128-d vector encoding or Base64 encoding of the user's face.
        /// Stored as a string (e.g., JSON array of floats) for easy DB saving.
        /// </summary>
        public string? FaceEncoding { get; set; }
    }
}
