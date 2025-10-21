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
        public IActionResult Index(string selectedGroup, string searchTerm)
        {
            var adminUsername = User.Identity?.Name;
            if (string.IsNullOrEmpty(adminUsername))
            {
                return Unauthorized("Cannot determine the current user.");
            }

            var samAccountName = adminUsername.Contains('\\') ? adminUsername.Split('\\')[1] : adminUsername;

            _logger.LogInformation("Fetching data for admin: {admin}", samAccountName);

            var managedGroups = _adManagementService.GetManagedGroupNamesForAdmin(samAccountName);

            // Truyền tham số lọc vào service
            List<UserPrincipal> managedUsers = _adManagementService.GetManagedUsersForAdmin(samAccountName, selectedGroup, searchTerm);

            var userViewModels = managedUsers.Select(user =>
            {
                // ... (logic tính ngày hết hạn không thay đổi)
                DateTime? expirationDate = null;
                if (user.PasswordNeverExpires == false)
                {
                    try
                    {
                        var de = user.GetUnderlyingObject() as DirectoryEntry;
                        if (de != null)
                        {
                            var expiryTimeComputed = de.Properties["msDS-UserPasswordExpiryTimeComputed"].Value;
                            if (expiryTimeComputed != null && expiryTimeComputed is long expiryTicks)
                            {
                                if (expiryTicks > 0 && expiryTicks != 9223372036854775807)
                                {
                                    expirationDate = DateTime.FromFileTime(expiryTicks);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not determine password expiration for user {user}", user.SamAccountName);
                        expirationDate = null;
                    }
                }

                return new UserViewModel
                {
                    Username = user.SamAccountName,
                    DisplayName = user.DisplayName,
                    EmailAddress = user.EmailAddress,
                    IsPasswordNeverExpires = user.PasswordNeverExpires,
                    IsPasswordChangeRequired = (user.LastPasswordSet == null),
                    PasswordExpirationDate = expirationDate
                };
            }).ToList();

            var viewModel = new UserManagementViewModel
            {
                Users = userViewModels,
                ManagedGroups = managedGroups,
                SelectedGroup = selectedGroup, // Gửi nhóm đang chọn xuống View
                SearchTerm = searchTerm // Gửi từ khóa tìm kiếm xuống View
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


        // GET: Management/CreateUser
        public IActionResult EditUser(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required to edit user.");
            }

            var user = _adManagementService.GetUserByUsername(username);
            if (user == null)
            {
                return NotFound($"User '{username}' not found.");
            }

            var model = new CreateUserViewModel
            {
                Username = user.SamAccountName,
                EmailAddress = user.EmailAddress,
                FirstName = user.GivenName,
                LastName = user.Surname,
                
                //SelectedOU = user.DistinguishedName,
                SetPasswordNeverExpires = user.PasswordNeverExpires,
                //RequirePasswordChangeOnLogon = user.PasswordNeverExpires,
                AvailableOUs = _adManagementService.GetAllOUs()
                    .Select(ou => new SelectListItem
                    {
                        Text = ou,
                        Value = ou,
                        Selected = ou == user.DistinguishedName // đánh dấu OU hiện tại
                    }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditUser(CreateUserViewModel model)
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
                bool isSuccess = _adManagementService.EditUser(model.Username, model.EmailAddress, model.FirstName, model.LastName,
                    model.Password, model.SelectedOU, model.RequirePasswordChangeOnLogon, model.SetPasswordNeverExpires);

                if (isSuccess)
                {
                    // View sẽ dùng JavaScript để gửi thông điệp về cho trang chính
                    ViewBag.ResetSuccess = true;
                    TempData["SuccessMessage"] = $"Edit user '{model.Username}' is successfully.";
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

        // GET: Management/Delete/username
        public async Task<IActionResult> Delete(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return NotFound();
            }

            // Gọi service để lấy thông tin người dùng từ AD hoặc database
            var user = _adManagementService.GetUserByUsername(username);
            if (user == null)
            {
                return NotFound($"User '{username}' not found.");
            }

            // Tạo ViewModel để hiển thị thông tin xác nhận xóa
            var model = new DeleteUserViewModel
            {
                Username = user.SamAccountName,
                EmailAddress = user.EmailAddress
            };

            // Trả về view xác nhận xóa
            return View(model);
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return NotFound("Username is required.");
            }

            var user = _adManagementService.GetUserByUsername(username);
            if (user == null)
            {
                ViewBag.DeleteSuccess = false;
                ViewBag.ErrorMessage = $"User '{username}' not found in Active Directory.";
                return View("Delete", new DeleteUserViewModel { Username = username });
            }

            bool deleted = _adManagementService.DeleteUser(username);
            if (deleted)
            {
                TempData["SuccessMessage"] = $"User '{username}' deleted successfully.";
                ViewBag.DeleteSuccess = true;
            }
            else
            {
                ViewBag.DeleteSuccess = false;
                ViewBag.ErrorMessage = $"Failed to delete user '{username}'.";
            }

            var model = new DeleteUserViewModel
            {
                Username = user.SamAccountName,
                EmailAddress = user.EmailAddress
            };

            return View("Delete", model);
        }

        // POST: Management/DeleteMultiple
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteMultiple(IEnumerable<string> usernames)
        {
            if (usernames == null || !usernames.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one user to delete.";
                return RedirectToAction(nameof(Index));
            }

            var deletedUsers = new List<DeleteUserViewModel>();
            var failedUsers = new List<string>();

            foreach (var username in usernames)
            {
                var user = _adManagementService.GetUserByUsername(username);
                if (user == null)
                {
                    failedUsers.Add(username);
                    continue;
                }

                bool deleted = _adManagementService.DeleteUser(username);
                if (deleted)
                {
                    deletedUsers.Add(new DeleteUserViewModel
                    {
                        Username = user.SamAccountName,
                        EmailAddress = user.EmailAddress
                    });
                }
                else
                {
                    failedUsers.Add(username);
                }
            }

            ViewBag.DeleteSuccess = deletedUsers.Any();
            ViewBag.FailedUsers = failedUsers;
            TempData["SuccessMessage"] = $"{deletedUsers.Count} user(s) deleted successfully.";
            TempData["ErrorMessage"] = failedUsers.Any()
                ? $"{failedUsers.Count} user(s) could not be deleted."
                : null;

            return View("DeleteMultipleConfirmation", deletedUsers);
        }


        /// GET: Management/DeleteMultipleConfirmation
        public IActionResult DeleteMultipleConfirmation(IEnumerable<string> usernames)
        {
            if (usernames == null || !usernames.Any())
            {
                return BadRequest("No users selected for deletion.");
            }

            var usersToDelete = new List<DeleteUserViewModel>();

            foreach (var username in usernames)
            {
                var user = _adManagementService.GetUserByUsername(username);
                if (user != null)
                {
                    usersToDelete.Add(new DeleteUserViewModel
                    {
                        Username = user.SamAccountName,
                        EmailAddress = user.EmailAddress
                    });
                }
            }

            if (!usersToDelete.Any())
            {
                return NotFound("No valid users found for deletion.");
            }

            // Truyền danh sách người dùng đến View để xác nhận
            return View(usersToDelete);
        }


    }
}