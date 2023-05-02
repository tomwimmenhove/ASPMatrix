namespace ASPMatrix.WebServer;

public class WebServerConfig
{
    // The name of the service (and, therefore, the name of the .dll file)
    public string Name { get; }

    // The location of the service on disk
    public string ServicePath { get; }

    // The url the service should be reachable at trough the reverse proxy
    public string UrlPath  { get; }

    public WebServerConfig(string name, string servicePath, string urlPath)
    {
        Name = name;
        ServicePath = servicePath;
        UrlPath = urlPath;
    }
}
