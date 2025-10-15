using ADPasswordManager.Constants;
using ADPasswordManager.Data;
using ADPasswordManager.Models.Entities;
using ADPasswordManager.Models.ViewModels; // Thêm using
using ADPasswordManager.Services;         // Thêm using
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Thêm using
using Microsoft.EntityFrameworkCore;
using System.Runtime.Versioning;         // Thêm using

namespace ADPasswordManager.Controllers
{
    [Authorize(Roles = Roles.SuperAdmin)]
    [SupportedOSPlatform("windows")] // Thêm attribute này
    public class SuperAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SuperAdminController> _logger;
        private readonly ADManagementService _adManagementService; // Thêm service

        // Cập nhật constructor
        public SuperAdminController(ApplicationDbContext context, ILogger<SuperAdminController> logger, ADManagementService adManagementService)
        {
            _context = context;
            _logger = logger;
            _adManagementService = adManagementService;
        }

        public async Task<IActionResult> Index()
        {
            var rules = await _context.DelegationRules.OrderBy(r => r.AdminGroup).ToListAsync();
            return View(rules);
        }

        // GET: SuperAdmin/Create
        public IActionResult Create()
        {
            var allGroups = _adManagementService.GetAllGroupNames();
            // Truyền danh sách group qua ViewBag thay vì Model
            ViewBag.AllAdGroups = new SelectList(allGroups);

            return View(new RuleViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RuleViewModel model)
        {
            if (string.IsNullOrEmpty(model.AdminGroup))
            {
                ModelState.AddModelError("AdminGroup", "The Admin Group field is required.");
            }
            if (model.SelectedManagedGroups == null || !model.SelectedManagedGroups.Any())
            {
                ModelState.AddModelError("SelectedManagedGroups", "Please select at least one managed group.");
            }

            if (ModelState.IsValid)
            {
                var newRule = new DelegationRule
                {
                    AdminGroup = model.AdminGroup,
                    ManagedGroups = string.Join(",", model.SelectedManagedGroups)
                };

                _context.DelegationRules.Add(newRule);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "New delegation rule created successfully.";
                ViewBag.SaveSuccess = true; // Báo cho View biết đã lưu thành công
                                            // return View(model); // Vẫn return View để script có thể chạy
            }

            // Nếu có lỗi, tải lại danh sách group và hiển thị lại form
            var allGroups = _adManagementService.GetAllGroupNames();
            ViewBag.AllAdGroups = new SelectList(allGroups);
            return View(model);
        }

        // GET: SuperAdmin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rule = await _context.DelegationRules.FindAsync(id);
            if (rule == null)
            {
                return NotFound();
            }

            var allGroups = _adManagementService.GetAllGroupNames();
            ViewBag.AllAdGroups = new SelectList(allGroups);

            var viewModel = new RuleViewModel
            {
                Id = rule.Id,
                AdminGroup = rule.AdminGroup,
                SelectedManagedGroups = rule.ManagedGroups.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            };

            // Trả về một PartialView để hiển thị trong iframe
            return PartialView("_RuleForm", viewModel);
        }

        // POST: SuperAdmin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RuleViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (model.SelectedManagedGroups == null || !model.SelectedManagedGroups.Any())
            {
                ModelState.AddModelError("SelectedManagedGroups", "Please select at least one managed group.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var ruleToUpdate = await _context.DelegationRules.FindAsync(id);
                    if (ruleToUpdate == null)
                    {
                        return NotFound();
                    }

                    ruleToUpdate.AdminGroup = model.AdminGroup;
                    ruleToUpdate.ManagedGroups = string.Join(",", model.SelectedManagedGroups);

                    _context.Update(ruleToUpdate);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Rule updated successfully.";
                    ViewBag.SaveSuccess = true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    ModelState.AddModelError(string.Empty, "The rule was modified by another user. Please try again.");
                }
            }

            var allGroups = _adManagementService.GetAllGroupNames();
            ViewBag.AllAdGroups = new SelectList(allGroups);
            return PartialView("_RuleForm", model);
        }

        // GET: SuperAdmin/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rule = await _context.DelegationRules.FirstOrDefaultAsync(m => m.Id == id);
            if (rule == null)
            {
                return NotFound();
            }

            // Trả về view xác nhận xóa
            return View(rule);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rule = await _context.DelegationRules.FindAsync(id);
            if (rule != null)
            {
                _context.DelegationRules.Remove(rule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Rule deleted successfully.";
            }

            // Thay vì Redirect, báo cho View biết đã xóa thành công
            ViewBag.DeleteSuccess = true;
            // Trả về chính View xác nhận, View này sẽ chứa script để gửi tín hiệu về trang cha
            return View("Delete", rule);
        }
        // POST: SuperAdmin/DeleteMultiple
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMultiple(IEnumerable<int> ruleIds)
        {
            if (ruleIds == null || !ruleIds.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one rule to delete.";
                return RedirectToAction(nameof(Index));
            }

            var rulesToDelete = await _context.DelegationRules
                                              .Where(r => ruleIds.Contains(r.Id))
                                              .ToListAsync();

            if (rulesToDelete.Any())
            {
                _context.DelegationRules.RemoveRange(rulesToDelete);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{rulesToDelete.Count} rule(s) deleted successfully.";
            }

            // Thay vì Redirect, báo cho View biết đã xóa thành công
            ViewBag.DeleteSuccess = true;
            // Trả về chính View xác nhận, View này sẽ chứa script để gửi tín hiệu về trang cha
            return View("DeleteMultipleConfirmation", rulesToDelete);
        }

        // GET: SuperAdmin/DeleteMultipleConfirmation
        public async Task<IActionResult> DeleteMultipleConfirmation(IEnumerable<int> ruleIds)
        {
            if (ruleIds == null || !ruleIds.Any())
            {
                // Trả về lỗi nếu không có ID nào được chọn
                return BadRequest("No rules selected for deletion.");
            }

            var rulesToDelete = await _context.DelegationRules
                                              .Where(r => ruleIds.Contains(r.Id))
                                              .ToListAsync();

            // Truyền danh sách các quy tắc sắp bị xóa đến View
            return View(rulesToDelete);
        }

        // function chèn trước dấu này
    }
}