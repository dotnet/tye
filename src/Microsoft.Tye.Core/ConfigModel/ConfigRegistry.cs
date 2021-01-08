using System.ComponentModel.DataAnnotations;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigRegistry
    {
        [Required]
        public string Hostname { get; set; } = null!;

        public string? PullSecret { get; set; }
    }
}
