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
    }
}
