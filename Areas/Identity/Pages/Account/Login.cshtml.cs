// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using ADPasswordManager.Services;
using System.Security.Claims;
using ADPasswordManager.Constants;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using ADPasswordManager.Data;

namespace ADPasswordManager.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [SupportedOSPlatform("windows")] // <-- Thêm attribute này
    public class LoginModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ADAuthenticationService adAuthService;
        private readonly IConfiguration _configuration; // <-- Thêm IConfiguration
        private readonly ApplicationDbContext _context;

        public LoginModel(SignInManager<IdentityUser> signInManager,
            ILogger<LoginModel> logger,
            UserManager<IdentityUser> userManager,
            ADAuthenticationService adAuthService,
            IConfiguration configuration, ApplicationDbContext context) // <-- Thêm IConfiguration vào constructor
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            this.adAuthService = adAuthService;
            _configuration = configuration; // <-- Gán giá trị
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            _logger.LogInformation("returnUrl: {returnUrl}", returnUrl);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                if (adAuthService.IsValid(Input.Email, Input.Password))
                {
                    var user = await _userManager.FindByNameAsync(Input.Email);
                    if (user == null)
                    {
                        user = new IdentityUser { UserName = Input.Email, Email = Input.Email, EmailConfirmed = true };
                        var result = await _userManager.CreateAsync(user);
                        if (!result.Succeeded)
                        {
                            ModelState.AddModelError(string.Empty, "Could not create local user account.");
                            return Page();
                        }
                    }

                    // --- BẮT ĐẦU LOGIC GÁN VAI TRÒ (ĐÃ SỬA) ---

                    // Xóa các claim vai trò cũ (nếu có) để đảm bảo sạch sẽ
                    var existingClaims = await _userManager.GetClaimsAsync(user);
                    await _userManager.RemoveClaimsAsync(user, existingClaims);

                    Claim newRoleClaim = null; // Khởi tạo là null
                    bool isSuperAdmin = false;
                    bool isDelegatedAdmin = false;

                    // Lấy thông tin tài khoản dịch vụ
                    string serviceUser = _configuration.GetValue<string>("ADSettings:ServiceUser");
                    string servicePassword = _configuration.GetValue<string>("ADSettings:ServicePassword");
                    string domain = _configuration.GetValue<string>("ADSettings:Domain");

                    try
                    {
                        using (var pc = new PrincipalContext(ContextType.Domain, domain, serviceUser, servicePassword))
                        {
                            var userPrincipal = UserPrincipal.FindByIdentity(pc, IdentityType.SamAccountName, Input.Email);
                            if (userPrincipal != null)
                            {
                                // 1. Kiểm tra SuperAdmin
                                string superAdminGroup = _configuration.GetValue<string>("ADSettings:SuperAdminGroup");
                                if (!string.IsNullOrEmpty(superAdminGroup))
                                {
                                    var groupPrincipal = GroupPrincipal.FindByIdentity(pc, superAdminGroup);
                                    if (groupPrincipal != null && userPrincipal.IsMemberOf(groupPrincipal))
                                    {
                                        isSuperAdmin = true;
                                    }
                                }

                                // 2. Nếu không phải SuperAdmin, kiểm tra DelegatedAdmin
                                if (!isSuperAdmin)
                                {
                                    // Lấy danh sách *tất cả* các nhóm admin từ DB
                                    var allAdminGroups = _context.DelegationRules.Select(r => r.AdminGroup).Distinct().ToList();

                                    foreach (var adminGroupName in allAdminGroups)
                                    {
                                        var delegGroup = GroupPrincipal.FindByIdentity(pc, adminGroupName);
                                        if (delegGroup != null && userPrincipal.IsMemberOf(delegGroup))
                                        {
                                            isDelegatedAdmin = true;
                                            break; // Chỉ cần thuộc 1 nhóm là đủ
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking group membership for user {user}", Input.Email);
                        ModelState.AddModelError(string.Empty, "Error verifying user roles.");
                        return Page();
                    }

                    // 3. Gán vai trò dựa trên kết quả kiểm tra
                    if (isSuperAdmin)
                    {
                        newRoleClaim = new Claim(ClaimTypes.Role, Roles.SuperAdmin);
                        await _userManager.AddClaimAsync(user, newRoleClaim);
                        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, new[] { newRoleClaim });

                        _logger.LogInformation("User {user} logged in with role {role}.", user.UserName, newRoleClaim.Value);
                        return RedirectToAction("Index", "SuperAdmin");
                    }
                    else if (isDelegatedAdmin)
                    {
                        newRoleClaim = new Claim(ClaimTypes.Role, Roles.DelegatedAdmin);
                        await _userManager.AddClaimAsync(user, newRoleClaim);
                        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, new[] { newRoleClaim });

                        _logger.LogInformation("User {user} logged in with role {role}.", user.UserName, newRoleClaim.Value);
                        return RedirectToAction("Index", "Management");
                    }
                    else
                    {
                        // Đây là "Regular User". Họ đăng nhập thành công, nhưng không có vai trò.
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation("User {user} logged in, but has no assigned role in this application.", user.UserName);

                        // Chuyển hướng họ đến trang "Access Denied".
                        return RedirectToAction("AccessDenied", "Home");
                    }
                    // --- KẾT THÚC LOGIC GÁN VAI TRÒ (ĐÃ SỬA) ---

                    _logger.LogInformation("User {user} logged in with role {role}.", user.UserName, newRoleClaim.Value);
                    _logger.LogDebug("User {user} logged in with role {role}.", user.UserName, newRoleClaim.Value);

                    // Kiểm tra xem người dùng có đang cố truy cập một trang cụ thể không
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
                    {
                        // Nếu có, chuyển hướng họ đến trang đó
                        return LocalRedirect(returnUrl);
                    }
                    else
                    {
                        // Nếu không, chuyển hướng dựa trên vai trò
                        if (newRoleClaim.Value == Roles.SuperAdmin)
                        {
                            return RedirectToAction("Index", "SuperAdmin");
                        }
                        else
                        {
                            return RedirectToAction("Index", "Management");
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            return Page();
        }
    }
}