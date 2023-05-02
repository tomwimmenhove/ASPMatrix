using ASPMatrix.Proxy;
using System.Text.Json;

namespace ASPMatrix.WebServer;

public class WebServers
{
    private readonly IProxyConfigurator _proxyConfigurator;
    private readonly List<WebServerInstance> _webServerInstances = new();
    private readonly string _configDirectoryPath;
    private readonly FileSystemWatcher _fileSystemWatcher;

    private struct WebServerInstance
    {
        public WebServerConfig WebServerConfig;
        public IWebServer WebServer;
        public int Port;
    }

    public WebServers(IProxyConfigurator proxyConfigurator, string configDirectoryPath)
    {
        _proxyConfigurator = proxyConfigurator;
        _configDirectoryPath = configDirectoryPath;

        _fileSystemWatcher = new FileSystemWatcher(configDirectoryPath, "*.json");

        _fileSystemWatcher.Created += OnConfigChanged;
        _fileSystemWatcher.Changed += OnConfigChanged;
        _fileSystemWatcher.Renamed += OnConfigChanged;
        _fileSystemWatcher.Deleted += OnConfigChanged;

        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    public async Task Start()
    {
        var configurations = ReadConfigurations().ToArray();
        await SetWebServers(configurations);
    }

    public async Task Stop()
    {
        foreach(var instance in _webServerInstances)
        {
            await instance.WebServer.Stop();
        }
    }

    private async void OnConfigChanged(object source, FileSystemEventArgs e)
    {
        Console.WriteLine("Reloading configuration");
        await Task.Delay(100);
        await Start();
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
                    Console.Error.WriteLine($"Failed to deserialize {filePath}");
                    continue;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to deserialize {filePath}: {e.Message}");
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
            Console.WriteLine($"Stopping {instance.WebServerConfig.Name} on {instance.WebServerConfig.UrlPath}");
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
                Console.WriteLine($"Starting {instance.WebServerConfig.Name} on {instance.WebServerConfig.UrlPath}");
                await instance.WebServer.Start();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to start {config.Name}: {e.Message}");
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
