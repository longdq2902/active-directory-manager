using Microsoft.AspNetCore.Mvc.Rendering;

namespace ADPasswordManager.Models.ViewModels
{
    public class UserManagementViewModel
    {
        public List<UserViewModel> Users { get; set; } = new List<UserViewModel>();
        //public List<string> ManagedGroups { get; set; } = new List<string>();
        //public string SelectedGroup { get; set; }

        public SelectList? AvailableOUs { get; set; }

        public string? SelectedOU { get; set; }
        public string SearchTerm { get; set; } // <-- Thêm dòng này
    }
}