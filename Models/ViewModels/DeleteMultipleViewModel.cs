namespace ADPasswordManager.Models.ViewModels
{
    public class DeleteMultipleViewModel
    {
        public List<string> UserIds { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
    }
}