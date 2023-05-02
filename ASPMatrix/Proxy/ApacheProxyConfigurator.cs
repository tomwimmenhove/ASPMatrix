using System.Diagnostics;
using System.Text;

namespace ASPMatrix.Proxy;

public class Apache2ProxyConfigurator : IProxyConfigurator
{
    private readonly string _proxyConfigPath;
    private readonly string _sudoPath;
    private readonly string _systemCtlPath;
    
    public Apache2ProxyConfigurator(string proxyConfigPath,
        string sudoPath = "/bin/sudo", string systemCtlPath = "/bin/systemctl")
    {
        _proxyConfigPath = proxyConfigPath;
        _sudoPath = sudoPath;
        _systemCtlPath = systemCtlPath;
    }

    public async Task SetProxyRules(IEnumerable<ProxyRule> rules)
    {
        var sb = new StringBuilder();

        foreach (var rule in rules)
        {
            sb.Append($"ProxyPass \"{rule.Path}\" \"{rule.TargetUrl}\"\n" +
                      $"ProxyPassReverse \"{rule.Path}\" \"{rule.TargetUrl}\"\n\n");
        }

        await File.WriteAllTextAsync(_proxyConfigPath, sb.ToString());

        await RestartApache2();
    }

    /* Make sure to have the following line in /etc/sudoers:
     * user ALL=(ALL) NOPASSWD: /bin/systemctl reload apache2.service
     * Where 'user' is the user this is running as */
    private async Task RestartApache2()
    {
        var process = new Process();
        process.StartInfo.FileName = _sudoPath ?? _systemCtlPath;
        process.StartInfo.Arguments = _sudoPath != null
            ? $"{_systemCtlPath} reload apache2.service"
            : "reload apache2.service";
        process.StartInfo.UseShellExecute = false;
        process.Start();
        
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{process.StartInfo.FileName} {process.StartInfo.Arguments} exited with error code {process.ExitCode}");
        }
    }
}

