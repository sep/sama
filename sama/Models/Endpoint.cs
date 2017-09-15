using System;
using System.ComponentModel.DataAnnotations;

namespace sama.Models
{
    public class Endpoint
    {
        public enum EndpointKind
        {
            Http = 0,
            Icmp = 1
        }

        [Key]
        public int Id { get; set; }

        [Required]
        public bool Enabled { get; set; }

        [Required]
        [StringLength(64, MinimumLength = 1)]
        public string Name { get; set; }

        [Required]
        public EndpointKind Kind { get; set; }

        [Required]
        public string JsonConfig { get; set; }

        [Required]
        public DateTimeOffset LastUpdated { get; set; }
    }
}
