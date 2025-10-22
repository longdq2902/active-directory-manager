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
    [Authorize]
    [SupportedOSPlatform("windows")]
    public class ManagementController : Controller
    {
        private readonly ILogger<ManagementController> _logger;
        private readonly ADManagementService _adManagementService;

        public ManagementController(ILogger<ManagementController> logger, ADManagementService adManagementService)
        {
            _logger = logger;
            _adManagementService = adManagementService;
        }

        // Thêm 2 tham số để nhận giá trị từ URL
        //public IActionResult Index(string selectedGroup, string searchTerm)
        //{
        //    var adminUsername = User.Identity?.Name;
        //    if (string.IsNullOrEmpty(adminUsername))
        //    {
        //        return Unauthorized("Cannot determine the current user.");
        //    }

        //    var samAccountName = adminUsername.Contains('\\') ? adminUsername.Split('\\')[1] : adminUsername;

        //    _logger.LogInformation("Fetching data for admin: {admin}", samAccountName);

        //    var managedGroups = _adManagementService.GetManagedGroupNamesForAdmin(samAccountName);

        //    // Truyền tham số lọc vào service
        //    List<UserPrincipal> managedUsers = _adManagementService.GetManagedUsersForAdmin(samAccountName, selectedGroup, searchTerm);

        //    var userViewModels = managedUsers.Select(user =>
        //    {
        //        // ... (logic tính ngày hết hạn không thay đổi)
        //        DateTime? expirationDate = null;
        //        if (user.PasswordNeverExpires == false)
        //        {
        //            try
        //            {
        //                var de = user.GetUnderlyingObject() as DirectoryEntry;
        //                if (de != null)
        //                {
        //                    var expiryTimeComputed = de.Properties["msDS-UserPasswordExpiryTimeComputed"].Value;
        //                    if (expiryTimeComputed != null && expiryTimeComputed is long expiryTicks)
        //                    {
        //                        if (expiryTicks > 0 && expiryTicks != 9223372036854775807)
        //                        {
        //                            expirationDate = DateTime.FromFileTime(expiryTicks);
        //                        }
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogWarning(ex, "Could not determine password expiration for user {user}", user.SamAccountName);
        //                expirationDate = null;
        //            }
        //        }

        //        return new UserViewModel
        //        {
        //            Username = user.SamAccountName,
        //            DisplayName = user.DisplayName,
        //            EmailAddress = user.EmailAddress,
        //            IsPasswordNeverExpires = user.PasswordNeverExpires,
        //            IsPasswordChangeRequired = (user.LastPasswordSet == null),
        //            PasswordExpirationDate = expirationDate
        //        };
        //    }).ToList();

        //    var viewModel = new UserManagementViewModel
        //    {
        //        Users = userViewModels,
        //        ManagedGroups = managedGroups,
        //        SelectedGroup = selectedGroup, // Gửi nhóm đang chọn xuống View
        //        SearchTerm = searchTerm // Gửi từ khóa tìm kiếm xuống View
        //    };

        //    return View(viewModel);
        //}
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
                IsPasswordChangeRequired = (user.LastPasswordSet == null)
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

        // code thêm vào trước chỗ này
    }
}