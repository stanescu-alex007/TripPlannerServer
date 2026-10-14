using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    /// <summary>
    /// A message from the contact page. Name and reply address are deliberately absent:
    /// the endpoint is for signed-in users only, so they come from the account and
    /// cannot be spoofed to make the mail look like it came from someone else.
    /// </summary>
    public class ContactRequest
    {
        [Required]
        [RegularExpression("^(bug|feature|question|partnership|other)$",
            ErrorMessage = "Subject must be one of: bug, feature, question, partnership, other.")]
        public string Subject { get; set; } = string.Empty;

        [Required, MinLength(20), MaxLength(2000)]
        public string Message { get; set; } = string.Empty;
    }
}
