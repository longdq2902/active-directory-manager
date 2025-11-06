namespace ADPasswordManager.Models.Configuration
{
    public class FeatureSettings
    {
        // Các giá trị mặc định là 'true'
        // Nếu mục này không tồn tại trong JSON, các tính năng sẽ mặc định được bật.
        public bool EnableCreateUser { get; set; } = true;
        public bool EnableDeleteUser { get; set; } = true;
        public bool EnableSendPasswordResetLink { get; set; } = true;
        public bool EnableSqlAccessToggle { get; set; } = false;
    }
}