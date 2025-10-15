using ADPasswordManager.Data; // Thêm dòng này
using ADPasswordManager.Models.ViewModels;
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

        // Cập nhật Constructor để nhận ApplicationDbContext
        public ADManagementService(ILogger<ADManagementService> logger, IConfiguration configuration, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context; // <-- Gán DbContext

            // Các cấu hình AD vẫn đọc từ appsettings.json
            _domain = configuration.GetValue<string>("ADSettings:Domain") ?? string.Empty;
            _serviceUser = configuration.GetValue<string>("ADSettings:ServiceUser") ?? string.Empty;
            _servicePassword = configuration.GetValue<string>("ADSettings:ServicePassword") ?? string.Empty;
        }

        // Thêm 2 tham số mới: selectedGroup và searchTerm
        public List<UserPrincipal> GetManagedUsersForAdmin(string adminUsername, string selectedGroup = null, string searchTerm = null)
        {
            _logger.LogDebug("--- Starting GetManagedUsersForAdmin for user: {user} with filter Group: '{group}', Search: '{search}' ---", adminUsername, selectedGroup, searchTerm);
            var managedUsers = new Dictionary<string, UserPrincipal>();

            var allManagedGroups = GetManagedGroupNamesForAdmin(adminUsername);
            if (!allManagedGroups.Any())
            {
                return new List<UserPrincipal>();
            }

            if (string.IsNullOrEmpty(_serviceUser) || string.IsNullOrEmpty(_servicePassword))
            {
                _logger.LogError("AD Service Account (ServiceUser/ServicePassword) is not configured in appsettings.json.");
                return new List<UserPrincipal>();
            }

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, _domain, _serviceUser, _servicePassword))
                {
                    // Nếu có chọn một nhóm cụ thể, chỉ lấy user từ nhóm đó
                    var groupsToScan = !string.IsNullOrEmpty(selectedGroup) && allManagedGroups.Contains(selectedGroup)
                        ? new List<string> { selectedGroup }
                        : allManagedGroups;

                    foreach (var groupName in groupsToScan)
                    {
                        var group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupName);
                        if (group != null)
                        {
                            var members = group.GetMembers(true);
                            foreach (var member in members)
                            {
                                if (member is UserPrincipal user)
                                {
                                    if (!managedUsers.ContainsKey(user.SamAccountName))
                                    {
                                        managedUsers.Add(user.SamAccountName, user);
                                    }
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Could not find managed group '{groupName}' in AD.", groupName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting managed users for '{adminUsername}'.", adminUsername);
            }

            var finalUserList = managedUsers.Values.AsEnumerable();

            // Áp dụng bộ lọc tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                finalUserList = finalUserList.Where(u =>
                    (u.SamAccountName != null && u.SamAccountName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (u.DisplayName != null && u.DisplayName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            _logger.LogDebug("--- Finished GetManagedUsersForAdmin. Found {count} unique users. ---", finalUserList.Count());
            return finalUserList.OrderBy(u => u.SamAccountName).ToList();
        }


        public List<string> GetManagedGroupNamesForAdmin(string adminUsername)
        {
            _logger.LogDebug("--- Starting GetManagedGroupNamesForAdmin for user: {user} ---", adminUsername);
            var groupsToManage = new HashSet<string>();
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
                            var managedGroupsFromRule = rule.ManagedGroups.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var managedGroup in managedGroupsFromRule)
                            {
                                groupsToManage.Add(managedGroup.Trim());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting managed groups for '{adminUsername}'.", adminUsername);
            }

            _logger.LogDebug("--- Finished GetManagedGroupNamesForAdmin. Found {count} unique groups. ---", groupsToManage.Count);
            return groupsToManage.OrderBy(g => g).ToList();
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






    }
}