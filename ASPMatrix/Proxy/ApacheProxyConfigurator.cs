using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace ASPMatrix.Proxy;

public class Apache2ProxyConfigurator : IProxyConfigurator
{
    private readonly ProxySettings _settings;
    
    public Apache2ProxyConfigurator(IOptions<ProxySettings> options)
    {
        _settings = options.Value;
    }

    public async Task SetProxyRules(IEnumerable<ProxyRule> rules)
    {
        var sb = new StringBuilder();

        foreach (var rule in rules)
        {
            sb.Append($"ProxyPass \"{rule.Path}\" \"{rule.TargetUrl}\"\n" +
                      $"ProxyPassReverse \"{rule.Path}\" \"{rule.TargetUrl}\"\n\n");
        }

        await File.WriteAllTextAsync(_settings.ProxyConfigFile, sb.ToString());

        await RestartApache2();
    }

    /* Make sure to have the following line in /etc/sudoers:
     * user ALL=(ALL) NOPASSWD: /bin/systemctl reload apache2.service
     * Where 'user' is the user this is running as */
    private async Task RestartApache2()
    {
        var process = new Process();
        process.StartInfo.FileName = _settings.SudoPath ?? _settings.SystemCtlPath;
        process.StartInfo.Arguments = _settings.SudoPath != null
            ? $"{_settings.SystemCtlPath} reload apache2.service"
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

