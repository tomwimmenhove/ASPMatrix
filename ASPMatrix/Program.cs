using ASPMatrix.Proxy;
using ASPMatrix.WebServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddOptions<ASPMatrix.Proxy.ProxySettings>()
            .BindConfiguration("Proxy")
            .ValidateDataAnnotations();
        services.AddOptions<ASPMatrix.WebServer.WebServerManagerSettings>()
            .BindConfiguration("WebServerManager")
            .ValidateDataAnnotations();

        services.AddSingleton<IProxyConfigurator, Apache2ProxyConfigurator>();
        services.AddHostedService<WebServerManagerHostedService>();
    });

using var host = hostBuilder.Build();

await host.RunAsync();

