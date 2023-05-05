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

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        var other = (WebServerConfig)obj;
        return Name == other.Name && ServicePath == other.ServicePath && UrlPath == other.UrlPath;
    }

    public override int GetHashCode() =>
        ((17 * 23 + ServicePath.GetHashCode()) * 23 + UrlPath.GetHashCode()) * 23 + Name.GetHashCode();
}
