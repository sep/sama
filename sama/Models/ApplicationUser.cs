using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sama.Models
{
    public class ApplicationUser
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [Display(Name = "User name")]
        public string UserName { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public string PasswordHashMetadata { get; set; }

        [NotMapped]
        public bool IsRemote { get; set; }
    }
}
