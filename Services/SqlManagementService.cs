using ADPasswordManager.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

// Sửa: Namespace phải là ADPasswordManager.Services
namespace ADPasswordManager.Services
{
    public class SqlManagementService : ISqlManagementService
    {
        private readonly ILogger<SqlManagementService> _logger;
        private readonly string _loginPrefix;

        public SqlManagementService(IConfiguration configuration, ILogger<SqlManagementService> logger)
        {
            _logger = logger;
            // Đọc LoginPrefix (ví dụ: "TRANSFER3") từ config
            _loginPrefix = configuration.GetValue<string>("SqlDelegationSettings:LoginPrefix");
            if (string.IsNullOrEmpty(_loginPrefix))
            {
                throw new InvalidOperationException("SqlDelegationSettings:LoginPrefix is not configured.");
            }
        }

        // Hàm helper để lấy tên login đầy đủ (ví dụ: "TRANSFER3\test.user")
        private string GetSqlLoginName(string adUsername) => $"{_loginPrefix}\\{adUsername}";

        public async Task<bool> CheckAccessAsync(string adUsername, string connectionString)
        {
            var sqlLogin = GetSqlLoginName(adUsername);
            // Kiểm tra xem USER (trong database) có tồn tại không
            var query = "SELECT 1 FROM sys.database_principals WHERE name = @loginName";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@loginName", sqlLogin);
                        var result = await command.ExecuteScalarAsync();
                        return (result != null); // Nếu tìm thấy -> return true
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check SQL access for {login}", sqlLogin);
                return false;
            }
        }

        public async Task GrantSqlAccessAsync(string adUsername, string connectionString)
        {
            var sqlLogin = GetSqlLoginName(adUsername);
            _logger.LogInformation("Attempting to grant SQL access to {login}", sqlLogin);

            // 1. CREATE LOGIN (Server level) từ Windows
            var query = $"IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = @loginName) " +
                        $"CREATE LOGIN [{sqlLogin}] FROM WINDOWS; " +

                        // 2. CREATE USER (Database level) cho Login đó
                        $"IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = @loginName) " +
                        $"CREATE USER [{sqlLogin}] FOR LOGIN [{sqlLogin}];";

            // 3. ĐÃ XÓA DÒNG LỖI "ALTER ROLE [public]..."

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@loginName", sqlLogin);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                _logger.LogInformation("Successfully granted SQL access to {login}", sqlLogin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to grant SQL access for {login}", sqlLogin);
                throw; // Ném lỗi ra để Controller bắt
            }
        }

        public async Task RevokeSqlAccessAsync(string adUsername, string connectionString)
        {
            var sqlLogin = GetSqlLoginName(adUsername);
            _logger.LogWarning("Attempting to REVOKE SQL access from {login}", sqlLogin);

            // 1. DROP USER (Database level)
            var query = $"IF EXISTS (SELECT name FROM sys.database_principals WHERE name = @loginName) " +
                        $"DROP USER [{sqlLogin}]; " +

                        // 2. DROP LOGIN (Server level)
                        $"IF EXISTS (SELECT name FROM sys.server_principals WHERE name = @loginName) " +
                        $"DROP LOGIN [{sqlLogin}];";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@loginName", sqlLogin);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                _logger.LogInformation("Successfully revoked SQL access from {login}", sqlLogin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke SQL access for {login}", sqlLogin);
                throw;
            }
        }
    }
}