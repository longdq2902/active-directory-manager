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

        public LoginModel(SignInManager<IdentityUser> signInManager,
            ILogger<LoginModel> logger,
            UserManager<IdentityUser> userManager,
            ADAuthenticationService adAuthService,
            IConfiguration configuration) // <-- Thêm IConfiguration vào constructor
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            this.adAuthService = adAuthService;
            _configuration = configuration; // <-- Gán giá trị
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

                    // --- BẮT ĐẦU LOGIC GÁN VAI TRÒ ---

                    // Xóa các claim vai trò cũ (nếu có) để đảm bảo sạch sẽ
                    var existingClaims = await _userManager.GetClaimsAsync(user);
                    var roleClaim = existingClaims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
                    if (roleClaim != null)
                    {
                        await _userManager.RemoveClaimAsync(user, roleClaim);
                    }

                    // Kiểm tra xem người dùng có phải là SuperAdmin không
                    string superAdminGroup = _configuration.GetValue<string>("ADSettings:SuperAdminGroup");
                    bool isSuperAdmin = false;

                    if (!string.IsNullOrEmpty(superAdminGroup))
                    {
                        try
                        {
                            // Sử dụng PrincipalContext để kiểm tra thành viên nhóm
                            using (var pc = new PrincipalContext(ContextType.Domain, _configuration.GetValue<string>("ADSettings:Domain")))
                            {
                                var userPrincipal = UserPrincipal.FindByIdentity(pc, IdentityType.SamAccountName, Input.Email);
                                if (userPrincipal != null)
                                {
                                    var groupPrincipal = GroupPrincipal.FindByIdentity(pc, superAdminGroup);
                                    if (groupPrincipal != null && userPrincipal.IsMemberOf(groupPrincipal))
                                    {
                                        isSuperAdmin = true;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error checking SuperAdmin group membership for user {user}", Input.Email);
                            // Xử lý lỗi (ví dụ: không thể kết nối AD), ở đây ta mặc định không phải super admin
                        }
                    }

                    // Gán claim vai trò tương ứng
                    var newRoleClaim = isSuperAdmin
                        ? new Claim(ClaimTypes.Role, Roles.SuperAdmin)
                        : new Claim(ClaimTypes.Role, Roles.DelegatedAdmin);

                    await _userManager.AddClaimAsync(user, newRoleClaim);

                    // Đăng nhập lại để claim có hiệu lực
                    await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, new[] { newRoleClaim });

                    // --- KẾT THÚC LOGIC GÁN VAI TRÒ ---

                    _logger.LogInformation("User {user} logged in with role {role}.", user.UserName, newRoleClaim.Value);
                    return LocalRedirect(returnUrl);
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