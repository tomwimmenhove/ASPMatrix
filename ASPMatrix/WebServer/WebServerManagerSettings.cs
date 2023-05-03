using System.ComponentModel.DataAnnotations;

namespace ASPMatrix.WebServer;

public class WebServerManagerSettings
{
    [Required]
    public string ServiceConfigPath { get; set; } = default!;
}
