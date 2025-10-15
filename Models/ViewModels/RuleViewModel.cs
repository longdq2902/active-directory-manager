using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ADPasswordManager.Models.ViewModels
{
    public class RuleViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Admin Group")]
        public string AdminGroup { get; set; }

        [Display(Name = "Managed Groups")]
        public List<string> SelectedManagedGroups { get; set; } = new List<string>();

        // Dòng dưới đây đã được XÓA
        // public SelectList AllAdGroups { get; set; } 
    }
}