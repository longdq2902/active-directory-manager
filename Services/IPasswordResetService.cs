namespace ADPasswordManager.Services
{
    public interface IPasswordResetService
    {
        Task<bool> GenerateAndSendResetLinkAsync(string username, string userEmail);
    }
}