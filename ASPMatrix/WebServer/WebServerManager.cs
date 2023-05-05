using ASPMatrix.Proxy;
using ASPMatrix.ConfigMonitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASPMatrix.WebServer;

public class WebServerManager : IHostedService, IDisposable
{
    private const string ConfigDirectory = "services-enabled";

    private bool disposed = false;
    private readonly IProxyConfigurator _proxyConfigurator;
    private readonly List<WebServerInstance> _webServerInstances = new();
    private readonly ConfigFileMonitor<WebServerConfig> _configFileMonitor;
    private readonly ILogger<WebServerManager> _logger;

    private struct WebServerInstance
    {
        public ConfigFile<WebServerConfig> ConfigFile;
        public IWebServer WebServer;
        public int Port;
    }

    public WebServerManager(ILogger<WebServerManager> logger,
        ConfigFileMonitor<WebServerConfig> configFileMonitor,
        IProxyConfigurator proxyConfigurator)
    {
        _logger = logger;
        _proxyConfigurator = proxyConfigurator;
        _configFileMonitor = configFileMonitor;

        _configFileMonitor.ConfigAdded += ConfigFileMonitorOnConfigAdded;
        _configFileMonitor.ConfigRemoved += ConfigFileMonitorOnConfigRemoved;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting server manager");

        _configFileMonitor.Start();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping server manager");

        foreach(var instance in _webServerInstances)
        {
            await instance.WebServer.Stop();
        }
    }

    async Task ConfigFileMonitorOnConfigAdded(object sender, ConfigFileEventArgs<WebServerConfig> e)
    {
        _logger.LogInformation($"Config added  : {e.ConfigFile.Config.Name} ({e.ConfigFile.FilePath})");

        var instance = CreateWebServerInstance(e.ConfigFile);
        _webServerInstances.Add(instance);

        await instance.WebServer.Start();
        await UpdateProxyRules();
    }

    async Task ConfigFileMonitorOnConfigRemoved(object sender, ConfigFileEventArgs<WebServerConfig> e)
    {
        _logger.LogInformation($"Config removed: {e.ConfigFile.Config.Name} ({e.ConfigFile.FilePath})");

        WebServerInstance? instance = _webServerInstances.FirstOrDefault(
            x => x.ConfigFile.FilePath == e.ConfigFile.FilePath);
        
        if (instance.HasValue)
        {
            await instance.Value.WebServer.Stop();
            _webServerInstances.Remove(instance.Value);
            await UpdateProxyRules();
        }
    }

    private async Task UpdateProxyRules()
    {
        var rules = _webServerInstances.Select(x => new ProxyRule(x.ConfigFile.Config.UrlPath, x.Port));
        await _proxyConfigurator.SetProxyRules(rules);
    }

    // private async Task SetWebServers(IList<WebServerConfig> webServerConfigs)
    // {
    //     var stopInstances = GetStopServices(webServerConfigs).ToArray();
    //     var tasks = new List<Task>();
    //     foreach(var instance in stopInstances)
    //     {
    //         _logger.LogInformation($"Stopping {instance.WebServerConfig.Name} on {instance.WebServerConfig.UrlPath}");
    //         var task = instance.WebServer.Stop();
    //         tasks.Add(task);

    //         _webServerInstances.Remove(instance);
    //     }

    //     await Task.WhenAll(tasks.ToArray());

    //     var startServices = webServerConfigs
    //         .Where(x => _webServerInstances.All(y => y.WebServerConfig.UrlPath != x.UrlPath))
    //         .ToArray();
    //     foreach(var config in startServices)
    //     {
    //         WebServerInstance instance;
    //         try
    //         {
    //             instance = CreateWebServerInstance(config);
    //             _logger.LogInformation($"Starting {instance.WebServerConfig.Name} on {instance.WebServerConfig.UrlPath}");
    //             await instance.WebServer.Start();
    //         }
    //         catch (Exception e)
    //         {
    //             _logger.LogError($"Failed to start {config.Name}: {e.Message}");
    //             continue;
    //         }

    //         _webServerInstances.Add(instance);
    //     }

    //     if (stopInstances.Any() || startServices.Any())
    //     {
    //         var rules =_webServerInstances.Select(x => new ProxyRule(x.WebServerConfig.UrlPath, x.Port));
    //         await _proxyConfigurator.SetProxyRules(rules);
    //     }
    // }

    // private IEnumerable<WebServerInstance> GetStopServices(IList<WebServerConfig> webServerConfigs)
    // {
    //     foreach(var instance in _webServerInstances)
    //     {
    //         var config = webServerConfigs.FirstOrDefault(x => x.UrlPath == instance.WebServerConfig.UrlPath);
    //         if (config == null)
    //         {
    //             yield return instance;
    //             continue;
    //         }

    //         if (config.ServicePath != instance.WebServerConfig.ServicePath || config.Name != instance.WebServerConfig.Name)
    //         {
    //             yield return instance;
    //         }
    //     }
    // }

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

    private WebServerInstance CreateWebServerInstance(ConfigFile<WebServerConfig> configFile)
    {
        var port = FindPort();
        var host = CoreWebHostBuilder.BuildLocalWebHost(
            configFile.Config.ServicePath, configFile.Config.Name, port);
        var webServer = new WebServerWebHost(host);

        return new WebServerInstance { ConfigFile = configFile, WebServer = webServer, Port = port };
    }

    ~WebServerManager()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                _configFileMonitor?.Dispose();
            }

            disposed = true;
        }
    }}
