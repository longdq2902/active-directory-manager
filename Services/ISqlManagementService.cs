using System.Threading.Tasks;

namespace ADPasswordManager.Services
{
    public interface ISqlManagementService
    {
        /// <summary>
        /// Cấp vai trò 'public' trên database 'master' cho một tài khoản Windows (AD).
        /// Tạo SQL Login và SQL User nếu chúng chưa tồn tại.
        /// </summary>
        /// <param name="username">Tên đăng nhập AD (sAMAccountName) của người dùng.</param>
        /// <param name="userOuDistinguishedName">Distinguished Name của OU chứa người dùng (để tìm SQL instance tương ứng).</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.Exception">Ném lỗi nếu OU không có cấu hình SQL instance, 
        /// không kết nối được SQL, hoặc thiếu quyền thực thi.</exception>
        Task GrantPublicRoleAsync(string username, string userOuDistinguishedName);

        // (Trong tương lai, có thể thêm các phương thức khác ở đây, 
        // ví dụ: RevokeSqlAccessAsync, CheckSqlAccessAsync, ...)
    }
}