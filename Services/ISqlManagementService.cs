using System.Threading.Tasks;

namespace ADPasswordManager.Services
{
    public interface ISqlManagementService
    {
        Task<bool> CheckAccessAsync(string adUsername, string connectionString);
        Task GrantSqlAccessAsync(string adUsername, string connectionString);
        Task RevokeSqlAccessAsync(string adUsername, string connectionString);
    }
}