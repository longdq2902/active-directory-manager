using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Cần cho Index

namespace ADPasswordManager.Models.Entities
{
    public class OuSqlInstanceMapping
    {
        // --- THÊM KHÓA CHÍNH ID ---
        [Key] // Đánh dấu đây là khóa chính
        public int Id { get; set; } // Tự động tăng theo mặc định
        // ------------------------

        // Distinguished Name của OU, ví dụ: "OU=Sales,DC=example,DC=com"
        [Required]
        // Chúng ta vẫn cần đảm bảo OU DN là duy nhất
        // Thuộc tính Index sẽ được cấu hình trong DbContext (Bước 1.2)
        public string OuDistinguishedName { get; set; }

        // Tên SQL Server instance, ví dụ: "SERVERNAME\SQLEXPRESS" hoặc "SERVERNAME"
        [Required]
        public string SqlInstanceName { get; set; }
    }
}