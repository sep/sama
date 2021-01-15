using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class EndpointViewModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public bool Enabled { get; set; }

        [Required]
        [StringLength(64, MinimumLength = 1)]
        public string? Name { get; set; }

        public Endpoint.EndpointKind Kind { get; set; }

        public string KindString { get { return Utility.KindString(Kind); } }
    }
}
