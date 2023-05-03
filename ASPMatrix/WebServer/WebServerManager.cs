using Microsoft.Extensions.Options;
using ASPMatrix.Proxy;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASPMatrix.WebServer;

public class WebServerManagerHostedService : IHostedService
{
    private const string ConfigDirectory = "services-enabled";

    private readonly IProxyConfigurator _proxyConfigurator;
    private readonly List<WebServerInstance> _webServerInstances = new();
    private readonly string _configDirectoryPath;
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly ILogger<WebServerManagerHostedService> _logger;
    private readonly System.Timers.Timer _onChangeTimer = new(100);

    private struct WebServerInstance
    {
        public WebServerConfig WebServerConfig;
        public IWebServer WebServer;
        public int Port;
    }

    public WebServerManagerHostedService(ILogger<WebServerManagerHostedService> logger,
        IProxyConfigurator proxyConfigurator, IOptions<WebServerManagerSettings> options)
    {
        _logger = logger;

        _proxyConfigurator = proxyConfigurator;
        _configDirectoryPath = Path.Combine(options.Value.ServiceConfigPath, ConfigDirectory);

        _onChangeTimer.Elapsed += OnChangeTimerOnElapsed;

        _fileSystemWatcher = new FileSystemWatcher(_configDirectoryPath, "*.json");

        _fileSystemWatcher.Created += OnConfigChanged;
        _fileSystemWatcher.Changed += OnConfigChanged;
        _fileSystemWatcher.Renamed += OnConfigChanged;
        _fileSystemWatcher.Deleted += OnConfigChanged;

        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting server manager");

        await Restart();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping server manager");

        foreach(var instance in _webServerInstances)
        {
            await instance.WebServer.Stop();
        }
    }

    private async Task Restart()
    {
        var configurations = ReadConfigurations().ToArray();
        await SetWebServers(configurations);
    }

    private async void OnChangeTimerOnElapsed(object? source, EventArgs e)
    {
        _onChangeTimer.Stop();
        _logger.LogInformation("Reloading configuration");
        await Restart();
    }

    private void OnConfigChanged(object? source, FileSystemEventArgs e)
    {
        _onChangeTimer.Start();
    }

    private IEnumerable<WebServerConfig> ReadConfigurations()
    {
        foreach (var filePath in Directory.GetFiles(_configDirectoryPath, "*.json"))
        {
            WebServerConfig? webServerConfig;
            try
            {
                var jsonData = File.ReadAllText(filePath);
                webServerConfig = JsonSerializer.Deserialize<WebServerConfig>(jsonData);
                if (webServerConfig == null)
                {
                    _logger.LogError($"Failed to deserialize {filePath}");
                    continue;
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to deserialize {filePath}: {e.Message}");
                continue;
            }

            yield return webServerConfig;
        }
    }

    private async Task SetWebServers(IList<WebServerConfig> webServerConfigs)
    {
        var stopInstances = GetStopServices(webServerConfigs).ToArray();
        var tasks = new List<Task>();
        foreach(var instance in stopInstances)
        {
            _logger.LogInformation($"Stopping {instance.WebServerConfig.Name} on {instance.WebServerConfig.UrlPath}");
            var task = instance.WebServer.Stop();
            tasks.Add(task);

            _webServerInstances.Remove(instance);
        }

        await Task.WhenAll(tasks.ToArray());

        var startServices = webServerConfigs
            .Where(x => _webServerInstances.All(y => y.WebServerConfig.UrlPath != x.UrlPath))
            .ToArray();
        foreach(var config in startServices)
        {
            WebServerInstance instance;
            try
            {
                instance = CreateWebServerInstance(config);
                _logger.LogInformation($"Starting {instance.WebServerConfig.Name} on {instance.WebServerConfig.UrlPath}");
                await instance.WebServer.Start();
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to start {config.Name}: {e.Message}");
                continue;
            }

            _webServerInstances.Add(instance);
        }

        if (stopInstances.Any() || startServices.Any())
        {
            var rules =_webServerInstances.Select(x => new ProxyRule(x.WebServerConfig.UrlPath, x.Port));
            await _proxyConfigurator.SetProxyRules(rules);
        }
    }

    private IEnumerable<WebServerInstance> GetStopServices(IList<WebServerConfig> webServerConfigs)
    {
        foreach(var instance in _webServerInstances)
        {
            var config = webServerConfigs.FirstOrDefault(x => x.UrlPath == instance.WebServerConfig.UrlPath);
            if (config == null)
            {
                yield return instance;
                continue;
            }

            if (config.ServicePath != instance.WebServerConfig.ServicePath || config.Name != instance.WebServerConfig.Name)
            {
                yield return instance;
            }
        }
    }

    private int FindPort()
    {
        for (int i = 5000; ; i++)
        {
            if (_webServerInstances.All(x => x.Port != i))
            {
                return i;
            }
        }
    }

    private WebServerInstance CreateWebServerInstance(WebServerConfig config)
    {
        var port = FindPort();
        var host = CoreWebHostBuilder.BuildLocalWebHost(config.ServicePath, config.Name, port);
        var webServer = new WebServerWebHost(host);

        return new WebServerInstance { WebServerConfig = config, WebServer = webServer, Port = port };
    }
}
