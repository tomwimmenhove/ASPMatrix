using System.ComponentModel.DataAnnotations;

namespace ASPMatrix.ConfigMonitoring;

public class ConfigFileSettings
{
    [Required]
    public string ConfigEnabled { get; set; } = default!;

    [Required]
    public string ConfigActive { get; set; } = default!;
}
