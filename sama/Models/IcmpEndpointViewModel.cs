using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class IcmpEndpointViewModel : EndpointViewModel
    {
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9-\.]{2,}$", ErrorMessage = "The Address field must contain a host or IP address (not a URL).")]
        public string Address { get; set; }
    }
}
