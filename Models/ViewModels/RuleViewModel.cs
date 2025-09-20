using System.ComponentModel.DataAnnotations;

namespace ADPasswordManager.Models.ViewModels
{
    public class RuleViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Admin Group")]
        public string AdminGroup { get; set; }

        [Required]
        [Display(Name = "Managed Groups (one per line)")]
        public string ManagedGroups { get; set; }
    }
}