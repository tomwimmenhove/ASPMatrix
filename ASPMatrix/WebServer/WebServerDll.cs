using Microsoft.AspNetCore.Hosting;

namespace ASPMatrix.WebServer;

public class WebServerWebHost : IWebServer
{
    private IWebHost _webHost;
    private Task _serverTask = Task.CompletedTask;

    public WebServerWebHost(IWebHost webHost)
    {
        _webHost = webHost;
    }

    public Task Start()
    {
        _serverTask = Task.Factory.StartNew(async () =>
        {
            await _webHost.RunAsync();        
        }, TaskCreationOptions.LongRunning);

        return Task.CompletedTask;
    }

    public async Task Stop()
    {
        await _webHost.StopAsync();
        await _serverTask;
    }
}
