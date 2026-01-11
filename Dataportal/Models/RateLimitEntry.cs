using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class RateLimitEntry
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Key { get; set; }

        public long Count { get; set; }

        public DateTime WindowEndUtc { get; set; }
    }
}