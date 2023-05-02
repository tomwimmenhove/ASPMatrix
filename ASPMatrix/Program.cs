using ASPMatrix.WebServer;
using ASPMatrix.Proxy;

var proxyConfigurator = new Apache2ProxyConfigurator("/etc/apache2/asp.net-proxy/reverse.conf");

string directoryPath = "services-enabled";
var servers = new WebServers(proxyConfigurator, directoryPath);

await servers.Start();

var waitHandle = new ManualResetEvent(false);
Console.CancelKeyPress += (sender, eventArgs) =>
{
    waitHandle.Set();
    eventArgs.Cancel = true;
};

new ManualResetEvent(false).WaitOne();

Console.Error.WriteLine("Stopping services");

await servers.Stop();

Console.WriteLine("Exiting");
