using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ADPasswordManager.Models.ViewModels
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "FirstName is required.")]
        public string? FirstName { get; set; }

        [Required(ErrorMessage = "NewLastName is required.")]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "EmailAddress is required.")]
        public string? EmailAddress { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }


        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select an Organizational Unit (OU).")]
        [Display(Name = "Organizational Unit")]
        public string SelectedOU { get; set; } = string.Empty;

        [Display(Name = "Password never expires")]
        public bool SetPasswordNeverExpires { get; set; } = false; // Mặc định là false (có hết hạn)

        [Display(Name = "User must change password at next logon")]
        public bool RequirePasswordChangeOnLogon { get; set; } = true; // Mặc định là true (phải đổi)

        public List<SelectListItem> AvailableOUs { get; set; } = new List<SelectListItem>();
    }
}
