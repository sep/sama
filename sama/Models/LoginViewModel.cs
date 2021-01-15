using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class LoginViewModel
    {
        [Required]
        public string? Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Required]
        [Display(Name = "Login Type")]
        public bool IsLocal { get; set; }
    }
}
