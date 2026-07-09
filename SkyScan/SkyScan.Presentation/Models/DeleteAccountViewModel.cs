using System.ComponentModel.DataAnnotations;

namespace SkyScan.Presentation.Models
{
    public class DeleteAccountViewModel
    {
        [Required(ErrorMessage = "Current password is required to delete your account.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string Password { get; set; } = string.Empty;
    }
}
