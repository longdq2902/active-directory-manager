using ADPasswordManager.Constants;
using ADPasswordManager.Data;
using ADPasswordManager.Models.Entities;
using ADPasswordManager.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ADPasswordManager.Controllers
{
    // Chỉ những ai có vai trò SuperAdmin mới được truy cập controller này
    [Authorize(Roles = Roles.SuperAdmin)]
    public class SuperAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SuperAdminController> _logger;

        public SuperAdminController(ApplicationDbContext context, ILogger<SuperAdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Action chính để hiển thị danh sách các quy tắc
        public async Task<IActionResult> Index()
        {
            var rules = await _context.DelegationRules.OrderBy(r => r.AdminGroup).ToListAsync();
            return View(rules);
        }
        // GET: SuperAdmin/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: SuperAdmin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RuleViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Chuyển đổi ManagedGroups từ chuỗi nhiều dòng thành chuỗi phân tách bằng dấu phẩy
                var managedGroupsString = string.Join(",", model.ManagedGroups
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim()));

                var newRule = new DelegationRule
                {
                    AdminGroup = model.AdminGroup,
                    ManagedGroups = managedGroupsString
                };

                _context.DelegationRules.Add(newRule);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "New delegation rule created successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
    }
}