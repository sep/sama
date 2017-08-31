using System;
using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class Setting
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Section { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Value { get; set; }
    }
}
