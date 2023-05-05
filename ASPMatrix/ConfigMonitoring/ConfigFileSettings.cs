using System.ComponentModel.DataAnnotations;

namespace ASPMatrix.ConfigMonitoring;

public class ConfigFileSettings
{
    [Required]
    public string ConfigPath { get; set; } = default!;
}
