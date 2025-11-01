using ADPasswordManager.Constants;
using ADPasswordManager.Models.ViewModels;
using ADPasswordManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

namespace ADPasswordManager.Controllers
{
    [Authorize(Roles = Roles.DelegatedAdmin)]
    [SupportedOSPlatform("windows")]
    public class ManagementController : Controller
    {
        private readonly ILogger<ManagementController> _logger;
        private readonly ADManagementService _adManagementService;
        private readonly IPasswordResetService _passwordResetService;

        public ManagementController(ILogger<ManagementController> logger, ADManagementService adManagementService, IPasswordResetService passwordResetService)
        {
            _logger = logger;
            _adManagementService = adManagementService;
            _passwordResetService = passwordResetService;
        }

    public async Task<IActionResult> Index(string? selectedOU, string? searchTerm)
        {
            var adminUsername = User.Identity?.Name;
            if (string.IsNullOrEmpty(adminUsername))
            {
                return Challenge(); // Hoặc redirect tới trang login
            }

            // GỌI HÀM MỚI: Lấy OUs thay vì Groups
            // (Đây là hàm chúng ta đã sửa ở Services/ADManagementService.cs)
            var managedOUs = _adManagementService.GetManagedOUNamesForAdmin(adminUsername);

            // GỌI HÀM MỚI: Truyền selectedOU thay vì selectedGroup
            // (Đây là hàm chúng ta đã sửa ở Services/ADManagementService.cs)
            var users = _adManagementService.GetManagedUsersForAdmin(adminUsername, selectedOU, searchTerm);

            // Map UserPrincipal sang UserViewModel (giữ nguyên)
            var userViewModels = users.Select(user => new UserViewModel
            {
                Username = user.SamAccountName,
                DisplayName = user.DisplayName,
                EmailAddress = user.EmailAddress,
                IsPasswordNeverExpires = user.PasswordNeverExpires,
                IsPasswordChangeRequired = (user.LastPasswordSet == null),
                IsEnabled = user.Enabled ?? false
            }).ToList();

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
                // GÁN VÀO AvailableOUs (thay vì AvailableGroups)
                AvailableOUs = new SelectList(ouSelectList, "Value", "Text", selectedOU),
                // GÁN VÀO SelectedOU (thay vì SelectedGroup)
                SelectedOU = selectedOU,
                SearchTerm = searchTerm
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

        // code thêm vào trước chỗ này
    }
}