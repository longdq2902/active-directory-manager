namespace ADPasswordManager.Models.ViewModels
{
    public class UserViewModel
    {
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? EmailAddress { get; set; }
        public bool IsPasswordNeverExpires { get; set; }
        public bool IsPasswordChangeRequired { get; set; }
        public DateTime? PasswordExpirationDate { get; set; }
        public bool IsEnabled { get; set; }
        public string? MappedSqlInstance { get; set; }
        public bool HasSqlAccess { get; set; } = false;
        public bool IsSqlMappingAvailable { get; set; } = false;
    }
}