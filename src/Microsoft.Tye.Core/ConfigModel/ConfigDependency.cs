using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigDependency
    {
        [Required]
        public string? Path { get; set; }
    }
}
