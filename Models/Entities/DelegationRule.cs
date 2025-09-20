using System.ComponentModel.DataAnnotations;

namespace ADPasswordManager.Models.Entities
{
    public class DelegationRule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string AdminGroup { get; set; }

        [Required]
        public string ManagedGroups { get; set; } // Sẽ lưu dưới dạng chuỗi các nhóm, phân tách bằng dấu phẩy
    }
}