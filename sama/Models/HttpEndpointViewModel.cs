using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class HttpEndpointViewModel : EndpointViewModel
    {
        [Required]
        [RegularExpression(@"^http[s]?://.+$", ErrorMessage = "The Location field must start with http:// or https:// and contain a host.")]
        public string Location { get; set; }

        [Display(Name = "Keyword Match")]
        public string ResponseMatch { get; set; }

        [Display(Name = "Status Codes")]
        [RegularExpression(@"^([0-9]{3}, ?)*[0-9]{3}$", ErrorMessage = "The Status Codes field must be a comma-separated list of HTTP status codes.")]
        public string StatusCodes { get; set; }
    }
}
