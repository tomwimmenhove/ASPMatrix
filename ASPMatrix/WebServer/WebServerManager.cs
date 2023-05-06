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

    private int FindPort() => Enumerable.Range(5000, Int16.MaxValue)
        .Except(_webServerInstances.Select(x => x.Port))
        .First();

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
