using Microsoft.AspNetCore.Mvc.Rendering;

namespace ADPasswordManager.Models.ViewModels
{
    public class UserManagementViewModel
    {
        public List<UserViewModel> Users { get; set; } = new List<UserViewModel>();

        public SelectList? AvailableOUs { get; set; }

        public string? SelectedOU { get; set; }
        public string SearchTerm { get; set; } 

        public bool EnableCreateUser { get; set; }
        public bool EnableDeleteUser { get; set; }
        public bool EnableSendPasswordResetLink { get; set; }
        public bool EnableSqlAccessToggle { get; set; }
    }
}