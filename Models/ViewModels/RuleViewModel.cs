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
        
        [Display(Name = "Managed OUs (for creating users)")]
        public string ManagedOUs { get; set; } = string.Empty; // Dùng string để nhận dữ liệu từ textarea

        // Dòng dưới đây đã được XÓA
        // public SelectList AllAdGroups { get; set; } 
    }
}