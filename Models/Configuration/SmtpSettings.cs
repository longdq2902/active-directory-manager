namespace ADPasswordManager.Models.Configuration
{
    public class SmtpSettings
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string SenderName { get; set; }
        public string SenderEmail { get; set; }
        public string SmtpUser { get; set; }
        public string SmtpPass { get; set; }
    }
}