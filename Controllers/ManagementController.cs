using ADPasswordManager.Constants;
using ADPasswordManager.Data;
using ADPasswordManager.Models.Configuration;
using ADPasswordManager.Models.ViewModels;
using ADPasswordManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; 
using System.Collections.Generic; 
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Extensions.Options; 

namespace ADPasswordManager.Controllers
{
    [Authorize(Roles = Roles.DelegatedAdmin)]
    [SupportedOSPlatform("windows")]
    public class ManagementController : Controller
    {
        private readonly ILogger<ManagementController> _logger;
        private readonly ADManagementService _adManagementService;
        private readonly IPasswordResetService _passwordResetService;
        private readonly ApplicationDbContext _context;
        private readonly ISqlManagementService _sqlService;
        private readonly IConfiguration _configuration;
        private readonly FeatureSettings _featureSettings; 

        public ManagementController(ILogger<ManagementController> logger, ADManagementService adManagementService, 
            IPasswordResetService passwordResetService, ApplicationDbContext context,
            ISqlManagementService sqlService, IConfiguration configuration, IOptions<FeatureSettings> featureSettings)
        {
            _logger = logger;
            _adManagementService = adManagementService;
            _passwordResetService = passwordResetService;
            _context = context;
            _sqlService = sqlService;
            _configuration = configuration;
            _featureSettings = featureSettings.Value;
        }

    public async Task<IActionResult> Index(string? selectedOU, string? searchTerm)
        {
            var adminUsername = User.Identity?.Name;
            if (string.IsNullOrEmpty(adminUsername))
            {
                return Challenge(); // Hoặc redirect tới trang login
            }

            
            var managedOUs = _adManagementService.GetManagedOUNamesForAdmin(adminUsername);

            //tim sql mapping
            string? mappedSqlInstance = null;
            string? sqlConnectionString = null; // Biến giữ chuỗi kết nối

            if (!string.IsNullOrEmpty(selectedOU))
            {
                var mapping = await _context.OuSqlInstanceMappings
                                            .FirstOrDefaultAsync(m => m.OuDistinguishedName == selectedOU);
                if (mapping != null)
                {
                    mappedSqlInstance = mapping.SqlInstanceName;
                    // Build connection string để kiểm tra
                    sqlConnectionString = BuildSqlConnectionString(mappedSqlInstance);
                }
            }


            // GỌI HÀM MỚI: Truyền selectedOU thay vì selectedGroup
            // (Đây là hàm chúng ta đã sửa ở Services/ADManagementService.cs)
            var users = _adManagementService.GetManagedUsersForAdmin(adminUsername, selectedOU, searchTerm);

            // Map UserPrincipal sang UserViewModel (giữ nguyên)
            var userViewModels = new List<UserViewModel>();

  
            foreach (var user in users)
            {
                bool hasSqlAccess = false;
                // Chỉ kiểm tra SQL nếu OU đã được map VÀ connection string build thành công
                if (sqlConnectionString != null && user.SamAccountName != null)
                {
                    hasSqlAccess = await _sqlService.CheckAccessAsync(user.SamAccountName, sqlConnectionString);
                }

                userViewModels.Add(new UserViewModel
                {
                    Username = user.SamAccountName,
                    DisplayName = user.DisplayName,
                    EmailAddress = user.EmailAddress,
                    IsPasswordNeverExpires = user.PasswordNeverExpires,
                    IsPasswordChangeRequired = (user.LastPasswordSet == null),
                    IsEnabled = user.Enabled ?? false,
                    MappedSqlInstance = mappedSqlInstance,
                    IsSqlMappingAvailable = (mappedSqlInstance != null),
                    HasSqlAccess = hasSqlAccess
                });
            }

            // THÊM MỚI: Hàm helper để tạo tên hiển thị "thân thiện" cho OU
            Func<string, string> formatOUName = (dn) =>
            {
                try
                {
                    // Input: "OU=Users,OU=Sales,DC=company,DC=com"
                    // Output: "Sales, Users"
                    var parts = dn.Split(',')
                                  .Where(p => p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                                  .Select(p => p.Substring(3))
                                  .Reverse();
                    return string.Join(", ", parts);
                }
                catch { return dn; } // Fallback
            };

            // THÊM MỚI: Tạo SelectList cho OUs
            var ouSelectList = managedOUs.Select(ou => new SelectListItem
            {
                Value = ou,
                Text = formatOUName(ou) // Sử dụng tên đã định dạng
            }).ToList();


            // SỬA: Gán dữ liệu vào ViewModel mới
            var viewModel = new UserManagementViewModel
            {
                Users = userViewModels,
                AvailableOUs = new SelectList(ouSelectList, "Value", "Text", selectedOU),
                SelectedOU = selectedOU,
                SearchTerm = searchTerm,
                EnableCreateUser = _featureSettings.EnableCreateUser,
                EnableDeleteUser = _featureSettings.EnableDeleteUser,
                EnableSendPasswordResetLink = _featureSettings.EnableSendPasswordResetLink,
                EnableSqlAccessToggle = _featureSettings.EnableSqlAccessToggle 

            };

            return View(viewModel);
        }

        // GET: /Management/ResetPassword?username=someuser
        public IActionResult ResetPassword(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Index");
            }

            var userStatus = _adManagementService.GetUserStatus(username);

            if (userStatus == null)
            {
                return NotFound($"User '{username}' not found.");
            }

            var model = new ResetPasswordViewModel
            {
                Username = userStatus.Username,
                SetPasswordNeverExpires = userStatus.IsPasswordNeverExpires,
                RequirePasswordChangeOnLogon = userStatus.IsPasswordChangeRequired
            };

            return View(model);
        }

        // POST: /Management/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool isSuccess = _adManagementService.ResetUserPassword(
                    model.Username,
                    model.NewPassword,
                    model.SetPasswordNeverExpires,
                    model.RequirePasswordChangeOnLogon);

                if (isSuccess)
                {
                    // Thay vì Redirect, chúng ta báo cho View biết là đã thành công
                    // View sẽ dùng JavaScript để gửi thông điệp về cho trang chính
                    ViewBag.ResetSuccess = true;
                    TempData["SuccessMessage"] = $"Password and options for user '{model.Username}' have been updated successfully.";
                    return View(model);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the user. Please check the application logs for details.");
                }
            }

            return View(model);
        }

        // GET: Management/CreateUser
        public IActionResult CreateUser()
        {
            if (!_featureSettings.EnableCreateUser) return Forbid();
            var model = new CreateUserViewModel
            {
                // Gọi service để lấy danh sách OU
                AvailableOUs = _adManagementService.GetAllOUs()
                         .Select(ou => new SelectListItem { Text = ou, Value = ou })
                         .ToList()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateUser(CreateUserViewModel model)
        {
            if (!_featureSettings.EnableCreateUser) return Forbid();
            if (string.IsNullOrEmpty(model.Username))
            {
                ModelState.AddModelError("Username", "The Username field is required.");
            }

            if (string.IsNullOrEmpty(model.FirstName))
            {
                ModelState.AddModelError("FirstName", "The FirstName field is required.");
            }

            if (string.IsNullOrEmpty(model.LastName))
            {
                ModelState.AddModelError("LastName", "The LastName field is required.");
            }

            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError("Password", "The Password field is required.");
            }

            if (string.IsNullOrEmpty(model.EmailAddress))
            {
                ModelState.AddModelError("EmailAddress", "The EmailAddress field is required.");
            }
            if (string.IsNullOrEmpty(model.SelectedOU))
            {
                ModelState.AddModelError("SelectedOU", "The Organizational Unit field is required.");
            }

            if (ModelState.IsValid)
            {
                bool isSuccess = _adManagementService.CreateUser(model.Username, model.EmailAddress, model.FirstName, model.LastName,
                    model.Password, model.SelectedOU,model.RequirePasswordChangeOnLogon,model.SetPasswordNeverExpires);

                if (isSuccess)
                {
                    // View sẽ dùng JavaScript để gửi thông điệp về cho trang chính
                    ViewBag.ResetSuccess = true;
                    TempData["SuccessMessage"] = $"Create user '{model.Username}' is successfully.";
                    return View(model);
                }  
                else
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while add the user. Please check the application logs for details.");
                }
                // Nếu ModelState không hợp lệ, phải nạp lại danh sách OU
                model.AvailableOUs = _adManagementService.GetAllOUs()
                            .Select(ou => new SelectListItem { Text = ou, Value = ou })
                            .ToList();

            }
            return View(model);
        }

        [HttpGet]
        public IActionResult DeleteMultipleConfirmation([FromQuery] List<string> userIds)
        {
            if (!_featureSettings.EnableDeleteUser) return Forbid();
            if (userIds == null || !userIds.Any())
            {
                return BadRequest("No users selected.");
            }
            var model = new DeleteMultipleViewModel { UserIds = userIds };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteMultiple(DeleteMultipleViewModel model)
        {
            if (!_featureSettings.EnableDeleteUser) return Forbid();
            if (model.UserIds == null || !model.UserIds.Any())
            {
                TempData["ErrorMessage"] = "No users were selected for deletion.";
                return RedirectToAction("Index");
            }

            var (Success, Message) = _adManagementService.DeleteUsers(model.UserIds);

            if (Success)
            {
                TempData["SuccessMessage"] = Message;
            }
            else
            {
                TempData["ErrorMessage"] = Message;
            }

            // Gửi thông điệp về iframe cha để đóng modal và tải lại trang
            return Content("<script>window.parent.postMessage('userSaved', '*');</script>", "text/html");
        }


        [HttpPost]
        [ValidateAntiForgeryToken] // Đảm bảo an toàn
        public async Task<IActionResult> SendResetLink(string username, string userEmail)
        {
            if (!_featureSettings.EnableSendPasswordResetLink)
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(userEmail))
            {
                return Json(new { success = false, message = "Username or Email is missing." });
            }

            try
            {
                // Gọi service chúng ta đã tạo ở Giai đoạn 2
                bool success = await _passwordResetService.GenerateAndSendResetLinkAsync(username, userEmail);

                if (success)
                {
                    _logger.LogInformation($"Admin successfully sent reset link to user '{username}'.");
                    return Json(new { success = true, message = $"Successfully sent reset link to {userEmail}." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to send email. Check system logs." });
                }
            }
            catch (Exception ex)
            {
                // Bắt lỗi nếu người dùng không có email (Exception chúng ta đã ném ra từ PasswordResetService)
                _logger.LogError(ex, $"Failed to send reset link for '{username}'.");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAccountStatus(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                // Trường hợp này ít xảy ra, nhưng set lỗi và reload
                TempData["ErrorMessage"] = "An error occurred: Username was missing.";
                return View("ReloadParent");
            }

            try
            {
                // 1. Gọi Service (Giai đoạn 1) để thực hiện hành động
                bool newStatus = _adManagementService.ToggleUserAccountStatus(username);
                string newStatusText = newStatus ? "Enabled" : "Disabled";

                // 2. Ghi log thành công
                _logger.LogInformation($"Admin successfully toggled account status for '{username}' to {newStatusText}.");

                // 3. Đặt thông báo thành công vào TempData
                // (Trang Index.cshtml sẽ tự động đọc và hiển thị)
                TempData["SuccessMessage"] = $"Account for '{username}' has been successfully {newStatusText}.";
            }
            catch (Exception ex)
            {
                // 4. Bắt lỗi (ví dụ: "User not found")
                _logger.LogError(ex, $"Failed to toggle account status for '{username}'.");

                // 5. Đặt thông báo lỗi vào TempData
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
            }

            // 6. Luôn trả về View "ReloadParent". 
            // View này sẽ gửi tin nhắn 'userSaved' lên trang cha (Index.cshtml).
            return View("ReloadParent");
        }



        [HttpGet]
        public IActionResult ToggleStatusConfirmation(string username, bool isEnabled)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return View("Error"); // Hoặc một view lỗi chung
            }

            // Truyền 2 giá trị này sang View
            ViewBag.Username = username;
            ViewBag.IsEnabled = isEnabled;

            return View(); // Sẽ trả về Views/Management/ToggleStatusConfirmation.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> ToggleSqlAccess(string username, string sqlInstance)
        {
            if (!_featureSettings.EnableSqlAccessToggle)
            {
                TempData["ErrorMessage"] = "This feature is currently disabled by the administrator.";
                return View("ReloadParent"); // Chặn và tải lại trang
            }
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(sqlInstance))
            {
                TempData["ErrorMessage"] = "An error occurred: Username or OU was missing.";
                return View("ReloadParent"); // Dùng lại view ReloadParent
            }

            try
            {
              

                var connectionString = BuildSqlConnectionString(sqlInstance);
                if (connectionString == null)
                {
                    throw new Exception("SQL Delegation admin credentials are not configured in appsettings.");
                }

                // 2. Kiểm tra trạng thái hiện tại
                bool currentAccess = await _sqlService.CheckAccessAsync(username, connectionString);

                // 3. Đảo ngược trạng thái
                if (currentAccess)
                {
                    // Đang có -> Thu hồi
                    await _sqlService.RevokeSqlAccessAsync(username, connectionString);
                    TempData["SuccessMessage"] = $"SQL access for '{username}' has been successfully REVOKED.";
                }
                else
                {
                    // Đang không có -> Cấp
                    await _sqlService.GrantSqlAccessAsync(username, connectionString);
                    TempData["SuccessMessage"] = $"SQL access for '{username}' has been successfully GRANTED.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to toggle SQL access for '{username}'.");
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
            }

            // 4. Luôn trả về View "ReloadParent" (y hệt ToggleAccountStatus)
            return View("ReloadParent");
        }


        [HttpGet]
        public IActionResult ToggleSqlAccessConfirmation(string username, string sqlInstance, bool hasSqlAccess)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return View("Error"); // Hoặc một view lỗi chung
            }
            _logger.LogInformation("User: {Username}, sqlInstance: {sqlInstance}, Current hasSqlAccess: {HasSqlAccess}",
        username, sqlInstance, hasSqlAccess);

            // Truyền 2 giá trị này sang View
            ViewBag.Username = username;
            ViewBag.sqlInstance = sqlInstance;
            ViewBag.HasSqlAccess = hasSqlAccess;

            return View(); // Sẽ trả về Views/Management/ToggleStatusConfirmation.cshtml
        }

        // Hàm Helper để build Connection String
        private string? BuildSqlConnectionString(string? instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
                return null;

            // Lấy thông tin đăng nhập SQL Admin từ config
            var sqlUser = _configuration["SqlDelegationSettings:AdminUser"];
            var sqlPass = _configuration["SqlDelegationSettings:AdminPassword"];
            var sqlDb = _configuration["SqlDelegationSettings:Database"];

            if (string.IsNullOrEmpty(sqlUser) || string.IsNullOrEmpty(sqlPass) || string.IsNullOrEmpty(sqlDb))
            {
                _logger.LogError("SqlDelegationSettings (AdminUser, AdminPassword, or Database) is not configured.");
                return null;
            }

            // Dùng User Id/Password, TrustServerCertificate=True để tránh lỗi SSL
            string conStr = $"Server={instanceName};Database={sqlDb};User Id={sqlUser};Password={sqlPass};TrustServerCertificate=True;";
            _logger.LogWarning("conStr:" + conStr);
            return conStr;
        }

        // code thêm vào trước chỗ này
    }
}