using ADPasswordManager.Data; // Thêm dòng này
using ADPasswordManager.Models.ViewModels;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

namespace ADPasswordManager.Services
{
    [SupportedOSPlatform("windows")]
    public class ADManagementService
    {
        private readonly ILogger<ADManagementService> _logger;
        private readonly ApplicationDbContext _context; // <-- Thay thế IConfiguration và DelegationSettings bằng DbContext
        private readonly string _domain;
        private readonly string _serviceUser;
        private readonly string _servicePassword;
        private readonly string _serviceOU;
        private readonly string _domainController;

        // Cập nhật Constructor để nhận ApplicationDbContext
        public ADManagementService(ILogger<ADManagementService> logger, IConfiguration configuration, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context; // <-- Gán DbContext

            // Các cấu hình AD vẫn đọc từ appsettings.json
            _domain = configuration.GetValue<string>("ADSettings:Domain") ?? string.Empty;
            _serviceUser = configuration.GetValue<string>("ADSettings:ServiceUser") ?? string.Empty;
            _servicePassword = configuration.GetValue<string>("ADSettings:ServicePassword") ?? string.Empty;
            _serviceOU = configuration.GetValue<string>("ADSettings:ServiceOU") ?? string.Empty;
            _domainController = configuration.GetValue<string>("ADSettings:DomainController") ?? string.Empty;
        }

        // Sửa đổi hoàn toàn hàm này
        [SupportedOSPlatform("windows")]
        public List<UserPrincipal> GetManagedUsersForAdmin(string adminUsername, string selectedOU = null, string searchTerm = null)
        {
            _logger.LogDebug("--- Starting GetManagedUsersForAdmin (by OU) for user: {user} with filter OU: '{ou}', Search: '{search}' ---", adminUsername, selectedOU, searchTerm);
            var managedUsers = new Dictionary<string, UserPrincipal>();

            // Gọi hàm mới ta vừa sửa
            var allManagedOUs = GetManagedOUNamesForAdmin(adminUsername);
            if (!allManagedOUs.Any())
            {
                _logger.LogWarning("Admin {user} has no managed OUs.", adminUsername);
                return new List<UserPrincipal>();
            }

            if (string.IsNullOrEmpty(_serviceUser) || string.IsNullOrEmpty(_servicePassword))
            {
                _logger.LogError("AD Service Account (ServiceUser/ServicePassword) is not configured in appsettings.json.");
                return new List<UserPrincipal>();
            }

            try
            {
                // Nếu có chọn một OU cụ thể, chỉ lấy user từ OU đó
                // Phải kiểm tra xem OU này có nằm trong danh sách admin được phép quản lý không
                var ousToScan = !string.IsNullOrEmpty(selectedOU) && allManagedOUs.Contains(selectedOU)
                    ? new List<string> { selectedOU }
                    : allManagedOUs; // Nếu không chọn gì, quét tất cả OUs được phép

                foreach (var ouDN in ousToScan)
                {
                    try
                    {
                        // Tạo context riêng cho từng OU
                        using (var context = new PrincipalContext(ContextType.Domain, _domain, ouDN, _serviceUser, _servicePassword))
                        using (var userPrincipalFilter = new UserPrincipal(context))
                        {
                            // Chỉ tìm kiếm trong phạm vi OU này (SearchScope.OneLevel hoặc Subtree tùy bạn)
                            // Mặc định PrincipalSearcher dùng Subtree, sẽ tìm cả các OU con
                            using (var searcher = new PrincipalSearcher(userPrincipalFilter))
                            {
                                foreach (var result in searcher.FindAll())
                                {
                                    if (result is UserPrincipal user)
                                    {
                                        if (!managedUsers.ContainsKey(user.SamAccountName))
                                        {
                                            managedUsers.Add(user.SamAccountName, user);
                                        }
                                        else
                                        {
                                            // Nếu user thuộc nhiều OU được quét, chúng ta chỉ cần giữ lại 1 bản
                                            // (giải phóng bản trùng lặp)
                                            user.Dispose();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error searching for users in OU: {OU}", ouDN);
                        // Bỏ qua nếu có lỗi ở 1 OU và tiếp tục với các OUs khác
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting managed users by OU for '{adminUsername}'.", adminUsername);
            }

            var finalUserList = managedUsers.Values.AsEnumerable();

            // Áp dụng bộ lọc tìm kiếm (searchTerm) sau khi đã lấy hết danh sách
            if (!string.IsNullOrEmpty(searchTerm))
            {
                finalUserList = finalUserList.Where(u =>
                    (u.SamAccountName != null && u.SamAccountName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (u.DisplayName != null && u.DisplayName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            _logger.LogDebug("--- Finished GetManagedUsersForAdmin (by OU). Found {count} unique users. ---", finalUserList.Count());
            // Trả về danh sách user, nhưng giải phóng các đối tượng UserPrincipal không nằm trong danh sách cuối cùng
            // (do searchTerm)
            var finalPrincipals = finalUserList.OrderBy(u => u.SamAccountName).ToList();

            // Giải phóng tài nguyên cho các user bị lọc ra
            foreach (var user in managedUsers.Values.Except(finalPrincipals))
            {
                user.Dispose();
            }

            return finalPrincipals;
        }


        //public List<string> GetManagedGroupNamesForAdmin(string adminUsername)
        //{
        //    _logger.LogDebug("--- Starting GetManagedGroupNamesForAdmin for user: {user} ---", adminUsername);
        //    var groupsToManage = new HashSet<string>();
        //    var allRules = _context.DelegationRules.ToList();

        //    if (string.IsNullOrEmpty(_domain) || !allRules.Any())
        //    {
        //        _logger.LogWarning("AD domain is not configured or no delegation rules found in the database.");
        //        return new List<string>();
        //    }

        //    try
        //    {
        //        using (var context = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
        //        {
        //            var adminUser = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, adminUsername);
        //            if (adminUser == null)
        //            {
        //                _logger.LogWarning("Could not find admin user '{adminUsername}' in AD.", adminUsername);
        //                return new List<string>();
        //            }

        //            var adminMemberOfGroups = adminUser.GetAuthorizationGroups();
        //            var adminGroupNames = new HashSet<string>(adminMemberOfGroups.Select(g => g.SamAccountName));

        //            foreach (var rule in allRules)
        //            {
        //                if (adminGroupNames.Contains(rule.AdminGroup))
        //                {
        //                    var managedGroupsFromRule = rule.ManagedGroups.Split(',', StringSplitOptions.RemoveEmptyEntries);
        //                    foreach (var managedGroup in managedGroupsFromRule)
        //                    {
        //                        groupsToManage.Add(managedGroup.Trim());
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An error occurred while getting managed groups for '{adminUsername}'.", adminUsername);
        //    }

        //    _logger.LogDebug("--- Finished GetManagedGroupNamesForAdmin. Found {count} unique groups. ---", groupsToManage.Count);
        //    return groupsToManage.OrderBy(g => g).ToList();
        //}
        public List<string> GetManagedOUNamesForAdmin(string adminUsername)
        {
            _logger.LogDebug("--- Starting GetManagedOUNamesForAdmin for user: {user} ---", adminUsername);

            // Đổi tên biến này
            var ousToManage = new HashSet<string>();
            var allRules = _context.DelegationRules.ToList();

            if (string.IsNullOrEmpty(_domain) || !allRules.Any())
            {
                _logger.LogWarning("AD domain is not configured or no delegation rules found in the database.");
                return new List<string>();
            }

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
                {
                    var adminUser = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, adminUsername);
                    if (adminUser == null)
                    {
                        _logger.LogWarning("Could not find admin user '{adminUsername}' in AD.", adminUsername);
                        return new List<string>();
                    }

                    var adminMemberOfGroups = adminUser.GetAuthorizationGroups();
                    var adminGroupNames = new HashSet<string>(adminMemberOfGroups.Select(g => g.SamAccountName));

                    foreach (var rule in allRules)
                    {
                        if (adminGroupNames.Contains(rule.AdminGroup))
                        {
                            // THAY ĐỔI LOGIC ĐỌC:
                            // 1. Đọc từ rule.ManagedOUs (thay vì ManagedGroups)
                            // 2. Split bằng dấu chấm phẩy ';' (thay vì ',')
                            var managedOUsFromRule = rule.ManagedOUs.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var managedOU in managedOUsFromRule)
                            {
                                ousToManage.Add(managedOU.Trim());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting managed OUs for '{adminUsername}'.", adminUsername);
            }

            _logger.LogDebug("--- Finished GetManagedOUNamesForAdmin. Found {count} unique OUs. ---", ousToManage.Count);
            return ousToManage.OrderBy(g => g).ToList();
        }

        public UserViewModel? GetUserStatus(string username)
        {
            _logger.LogDebug("Getting status for user '{username}'", username);
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
                {
                    var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);
                    if (user == null)
                    {
                        _logger.LogWarning("User '{username}' not found when trying to get status.", username);
                        return null;
                    }

                    var userViewModel = new UserViewModel
                    {
                        Username = user.SamAccountName,
                        DisplayName = user.DisplayName,
                        EmailAddress = user.EmailAddress,
                        IsPasswordNeverExpires = user.PasswordNeverExpires,
                        IsPasswordChangeRequired = (user.LastPasswordSet == null)
                    };
                    return userViewModel;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get status for user '{username}'", username);
                return null;
            }
        }

        public bool ResetUserPassword(string username, string newPassword, bool setNeverExpires, bool requireChange)
        {
            _logger.LogInformation("Attempting to reset password for user '{username}' with options: SetNeverExpires={setNeverExpires}, RequireChange={requireChange}", username, setNeverExpires, requireChange);

            try
            {
                using (var pContext = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
                {
                    var userPrincipal = UserPrincipal.FindByIdentity(pContext, IdentityType.SamAccountName, username);
                    if (userPrincipal == null)
                    {
                        _logger.LogWarning("User '{username}' not found. Password reset failed.", username);
                        return false;
                    }

                    userPrincipal.SetPassword(newPassword);
                    _logger.LogDebug("Password set in memory for '{username}'.", username);

                    userPrincipal.PasswordNeverExpires = setNeverExpires;
                    _logger.LogDebug("PasswordNeverExpires set to {val} for '{username}'.", setNeverExpires, username);

                    if (requireChange)
                    {
                        userPrincipal.ExpirePasswordNow();
                        _logger.LogDebug("Password for '{username}' has been set to expire.", username);
                    }

                    userPrincipal.UnlockAccount();
                    _logger.LogDebug("Account for '{username}' has been unlocked.", username);

                    userPrincipal.Save();
                    _logger.LogInformation("Successfully saved all changes for user '{username}'.", username);

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while resetting password for '{username}'", username);
                return false;
            }
        }




        [SupportedOSPlatform("windows")]
        public List<string> GetAllGroupNames()
        {
            _logger.LogDebug("--- Starting GetAllGroupNames ---");
            var groupNames = new List<string>();

            if (string.IsNullOrEmpty(_domain) || string.IsNullOrEmpty(_serviceUser) || string.IsNullOrEmpty(_servicePassword))
            {
                _logger.LogError("AD settings (Domain, ServiceUser, ServicePassword) are not fully configured.");
                return groupNames;
            }

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
                {
                    using (var searcher = new PrincipalSearcher(new GroupPrincipal(context)))
                    {
                        foreach (var result in searcher.FindAll())
                        {
                            if (result is GroupPrincipal group)
                            {
                                groupNames.Add(group.SamAccountName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all group names from AD.");
            }

            _logger.LogDebug("--- Finished GetAllGroupNames. Found {count} groups. ---", groupNames.Count);
            return groupNames.OrderBy(name => name).ToList();
        }

        public List<string> GetAllOUs()
        {
            _logger.LogDebug("--- Starting GetAllOUs ---");
            var ouList = new List<string>();

            if (string.IsNullOrEmpty(_domain) || string.IsNullOrEmpty(_serviceUser) || string.IsNullOrEmpty(_servicePassword))
            {
                _logger.LogError("AD settings (Domain, ServiceUser, ServicePassword) are not fully configured.");
                return ouList;
            }

            try
            {
                // Sử dụng PrincipalContext với root domain (không cần _serviceOU)
                using (var context = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
                {
                    // Dùng DirectorySearcher để tìm kiếm hiệu quả các OU
                    using (var de = new DirectoryEntry($"LDAP://{_domain}", _serviceUser, _servicePassword))
                    {
                        using (var searcher = new DirectorySearcher(de))
                        {
                            searcher.Filter = "(objectCategory=organizationalUnit)";
                            searcher.SearchScope = SearchScope.Subtree;
                            searcher.PropertiesToLoad.Add("distinguishedName");

                            foreach (SearchResult result in searcher.FindAll())
                            {
                                if (result.Properties.Contains("distinguishedName"))
                                {
                                    ouList.Add((string)result.Properties["distinguishedName"][0]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all OUs from AD.");
            }

            _logger.LogDebug("--- Finished GetAllOUs. Found {count} OUs. ---", ouList.Count);
            return ouList.OrderBy(name => name).ToList();
        }

        // Sửa lại phương thức CreateUser
        public bool CreateUser(string username, string email, string firstName, string lastName, string password, string selectedOU, bool requireChange, bool neverExpires) // Thêm tham số selectedOU
        {
            _logger.LogInformation("Attempting to create user '{username}' in OU: {ou}", username, selectedOU);
            try
            {
                // DÙNG selectedOU thay vì _serviceOU
                using (var pContext = new PrincipalContext(ContextType.Domain, _domain, selectedOU, _serviceUser, _servicePassword))
                {
                    var userPrincipal = UserPrincipal.FindByIdentity(pContext, IdentityType.SamAccountName, username);
                    if (userPrincipal != null)
                    {
                        _logger.LogWarning("User '{username}' already exists in this context. Create user failed.", username);
                        return false;
                    }

                    using (UserPrincipal user = new UserPrincipal(pContext))
                    {
                        user.SamAccountName = username;
                        user.EmailAddress = email;
                        user.DisplayName = $"{firstName} {lastName}"; // Thêm khoảng trắng
                        user.GivenName = firstName;
                        user.Surname = lastName;
                        user.Enabled = true;
                        user.SetPassword(password);
                        user.PasswordNeverExpires = neverExpires;

                        if (requireChange)
                        {
                            user.ExpirePasswordNow(); // Kích hoạt cờ "phải đổi mật khẩu"
                        }
                        user.Save();
                    }
                    ;

                    _logger.LogInformation("Successfully created user '{username}' in OU '{ou}'", username, selectedOU);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Sửa lại thông báo log cho đúng ngữ cảnh
                _logger.LogError(ex, "An error occurred while creating user '{username}'", username);
                return false;
            }
        }

        [SupportedOSPlatform("windows")]
        public (bool Success, string Message) DeleteUsers(List<string> usernames)
        {
            _logger.LogWarning("Attempting to delete {Count} users: {Usernames}", usernames.Count, string.Join(", ", usernames));
            int successCount = 0;
            int failCount = 0;

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
                {
                    foreach (var username in usernames)
                    {
                        try
                        {
                            var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);
                            if (user != null)
                            {
                                user.Delete();
                                _logger.LogInformation("Successfully deleted user '{Username}'", username);
                                successCount++;
                            }
                            else
                            {
                                _logger.LogWarning("Could not find user '{Username}' to delete.", username);
                                failCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete user '{Username}'", username);
                            failCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in DeleteUsers method.");
                return (false, $"An error occurred: {ex.Message}");
            }

            return (true, $"Successfully deleted {successCount} users. Failed to delete {failCount} users.");
        }



    }
}