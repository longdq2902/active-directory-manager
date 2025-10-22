using System.ComponentModel.DataAnnotations;

namespace ADPasswordManager.Models.ViewModels
{
    public class PublicResetViewModel
    {
        [Required]
        public string Token { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")] 
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")] 
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")] 
        public string ConfirmPassword { get; set; }
    }
}