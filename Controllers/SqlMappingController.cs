using ADPasswordManager.Constants;
using ADPasswordManager.Data;
using ADPasswordManager.Models.Configuration;
using ADPasswordManager.Models.Entities;
using ADPasswordManager.Models.ViewModels;
using ADPasswordManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Cần dùng cho 'SelectList'
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;


namespace ADPasswordManager.Controllers
{
    [Authorize(Roles = Roles.SuperAdmin)]
    [SupportedOSPlatform("windows")]
    public class SqlMappingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ADManagementService _adManagementService;
        private readonly ILogger<SqlMappingController> _logger;
        private readonly FeatureSettings _featureSettings;

        public SqlMappingController(ApplicationDbContext context, ADManagementService adManagementService, 
            ILogger<SqlMappingController> logger, IOptions<FeatureSettings> featureSettings)
        {
            _context = context;
            _adManagementService = adManagementService;
            _logger = logger;
            _featureSettings = featureSettings.Value; 
        }

        // GET: SqlMapping
        public async Task<IActionResult> Index()
        {
            if (!_featureSettings.EnableSqlAccessToggle) return NotFound(); 
            var mappings = await _context.OuSqlInstanceMappings.OrderBy(m => m.OuDistinguishedName).ToListAsync();
            return View(mappings);
        }

        // --- SỬA LẠI CREATE (GET) ---
        public IActionResult Create()
        {
            if (!_featureSettings.EnableSqlAccessToggle) return NotFound(); 
            var allOUs = _adManagementService.GetAllOUs();
            ViewBag.AllOUs = new SelectList(allOUs);
            return View(new SqlMappingViewModel());
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SqlMappingViewModel viewModel)
        {
            _logger.LogInformation("Create new SQL Instance Mapping Controller");
            _logger.LogInformation("--viewModel.OuDistinguishedName: " + viewModel.OuDistinguishedName);
            _logger.LogInformation("--viewModel.SqlInstanceName: " + viewModel.SqlInstanceName);
            if (string.IsNullOrEmpty(viewModel.OuDistinguishedName))
            {
                ModelState.AddModelError("OuDistinguishedName", "Please select an Organizational Unit.");
                //_logger.log("ModelState IsValid.");
            }
            if (string.IsNullOrEmpty(viewModel.SqlInstanceName))
            {
                ModelState.AddModelError("SqlInstanceName", "Please enter the SQL Instance Name.");

            }
            _logger.LogInformation("ModelState: "+ ModelState.IsValid.ToString());
            if (ModelState.IsValid)
            {
                _logger.LogInformation("ModelState IsValid.");
                try
                {
                    var mapping = new OuSqlInstanceMapping
                    {
                        OuDistinguishedName = viewModel.OuDistinguishedName,
                        SqlInstanceName = viewModel.SqlInstanceName
                    };
                    _context.Add(mapping);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "New SQL mapping created successfully.";
                    ViewBag.SaveSuccess = true;
                    _logger.LogInformation("New SQL mapping created successfully.");
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error creating SQL mapping.");
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE constraint"))
                    {
                        ModelState.AddModelError("OuDistinguishedName", "This OU is already mapped. Please edit the existing mapping.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "An error occurred while saving the mapping.");
                    }
                }
            }

            var allOUs = _adManagementService.GetAllOUs();
            ViewBag.AllOUs = new SelectList(allOUs);

            return View(viewModel);
        }

        // --- EDIT (GET) KHÔNG ĐỔI ---
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var mapping = await _context.OuSqlInstanceMappings.FindAsync(id);
            if (mapping == null) return NotFound();

            var viewModel = new SqlMappingViewModel
            {
                Id = mapping.Id,
                OuDistinguishedName = mapping.OuDistinguishedName,
                SqlInstanceName = mapping.SqlInstanceName
            };
            return View(viewModel);
        }

        // --- EDIT (POST) KHÔNG ĐỔI ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SqlMappingViewModel viewModel)
        {
            if (id != viewModel.Id) return NotFound();

            if (string.IsNullOrEmpty(viewModel.SqlInstanceName))
            {
                ModelState.AddModelError("SqlInstanceName", "Please enter the SQL Instance Name.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var mappingToUpdate = await _context.OuSqlInstanceMappings.FindAsync(id);
                    if (mappingToUpdate == null) return NotFound();
                    mappingToUpdate.SqlInstanceName = viewModel.SqlInstanceName;
                    _context.Update(mappingToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "SQL mapping updated successfully.";
                    ViewBag.SaveSuccess = true;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error updating SQL mapping.");
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the mapping.");
                }
            }
            return View(viewModel);
        }

        // --- DELETE (GET) KHÔNG ĐỔI ---
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var mapping = await _context.OuSqlInstanceMappings.FirstOrDefaultAsync(m => m.Id == id);
            if (mapping == null) return NotFound();
            return View(mapping);
        }

        // --- DELETE (POST) KHÔNG ĐỔI ---
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var mapping = await _context.OuSqlInstanceMappings.FindAsync(id);
            if (mapping != null)
            {
                _context.OuSqlInstanceMappings.Remove(mapping);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "SQL mapping deleted successfully.";
            }
            ViewBag.DeleteSuccess = true;
            return View("Delete", mapping);
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

            var rulesToDelete = await _context.OuSqlInstanceMappings
                                              .Where(r => ruleIds.Contains(r.Id))
                                              .ToListAsync();

            if (rulesToDelete.Any())
            {
                _context.OuSqlInstanceMappings.RemoveRange(rulesToDelete);
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

            var rulesToDelete = await _context.OuSqlInstanceMappings
                                              .Where(r => ruleIds.Contains(r.Id))
                                              .ToListAsync();

            // Truyền danh sách các quy tắc sắp bị xóa đến View
            return View(rulesToDelete);
        }

        //code mowis tren dong nay

    }
}