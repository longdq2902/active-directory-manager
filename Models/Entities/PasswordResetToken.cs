using System.ComponentModel.DataAnnotations;

namespace ADPasswordManager.Models.Entities
{
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public DateTime ExpiryTimestamp { get; set; }

        public bool IsUsed { get; set; } = false;
    }
}