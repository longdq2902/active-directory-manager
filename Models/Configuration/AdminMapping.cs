namespace ADPasswordManager.Models.Configuration
{
    public class AdminMapping
    {
        public string AdminGroup { get; set; } = string.Empty;
        public List<string> ManagedGroups { get; set; } = new();

        // ===== THÊM THUỘC TÍNH NÀY VÀO =====
        public string ManagedOUs { get; set; } = string.Empty; // ManagedOUs trong JSON là một string
    }
}