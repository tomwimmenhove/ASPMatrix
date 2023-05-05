using ASPMatrix.Proxy;
using ASPMatrix.WebServer;
using ASPMatrix.ConfigMonitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddOptions<ProxySettings>()
            .BindConfiguration("Proxy")
            .ValidateDataAnnotations();
        services.AddOptions<ConfigFileSettings>()
            .BindConfiguration("ConfigMonitor")
            .ValidateDataAnnotations();

        services.AddSingleton<ConfigFileMonitor<WebServerConfig>>();
        services.AddSingleton<IProxyConfigurator, Apache2ProxyConfigurator>();
        services.AddHostedService<WebServerManager>();
    });

using var host = hostBuilder.Build();

await host.RunAsync();
