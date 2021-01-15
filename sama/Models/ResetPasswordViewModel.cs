using System;
using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class ResetPasswordViewModel
    {
        [Key]
        public Guid UserId { get; set; }

        [Display(Name = "User name")]
        [Editable(false)]
        public string? UserName { get; set; }

        [Required]
        [Display(Name = "New password")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}
