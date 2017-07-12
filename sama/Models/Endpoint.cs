using System;
using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class Endpoint
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public bool Enabled { get; set; }

        [Required]
        [StringLength(64, MinimumLength = 1)]
        public string Name { get; set; }

        [Required]
        [RegularExpression(@"^http[s]?://.+$", ErrorMessage = "The Location field must start with http:// or https:// and contain a host.")]
        public string Location { get; set; }

        [Display(Name = "Keyword Match")]
        public string ResponseMatch { get; set; }
    }
}
