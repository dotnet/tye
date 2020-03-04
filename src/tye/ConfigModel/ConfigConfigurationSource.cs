using System.ComponentModel.DataAnnotations;

namespace Tye.ConfigModel
{
    public class ConfigConfigurationSource
    {
        [Required]
        public string Name { get; set; } = default!;
        [Required]
        public string Value { get; set; } = default!;
        public string? Source { get; set; }
    }
}
