using System.ComponentModel.DataAnnotations;

namespace ASPMatrix.Proxy;

public class ProxySettings
{
    [Required]
    public string ProxyConfigFile { get; set; } = default!;

    public string SudoPath { get; set; } = "/bin/sudo";

    public string SystemCtlPath { get; set; } = "/bin/systemctl";
}
