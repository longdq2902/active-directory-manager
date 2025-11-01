// Xóa: using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace ADPasswordManager.Models.ViewModels
{
    public class SqlMappingViewModel
    {
        public int Id { get; set; }

        // Xóa [Required] và [Display]
        public string OuDistinguishedName { get; set; }

        // Xóa [Required] và [Display]
        public string SqlInstanceName { get; set; }

        //public IEnumerable<SelectListItem> AllOUs { get; set; }
    }
}