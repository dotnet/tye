using System.ComponentModel.DataAnnotations;

namespace Tye.ConfigModel
{
    internal class ConfigServiceBinding
    {
        [Required]
        public string Name { get; set; } = default!;
        public string? ConnectionString { get; set; }
        public int? Port { get; set; }
        public int? InternalPort { get; set; }
        public string? Host { get; set; }
        public string? Protocol { get; set; }
    }
}
