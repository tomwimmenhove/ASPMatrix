namespace ASPMatrix.Proxy;

public class ProxyRule
{
    public string Path { get; }
    public string TargetUrl { get; }

    public ProxyRule(string path, string targetUrl)
    {
        Path = path;
        TargetUrl = targetUrl;
    }

    public ProxyRule(string path, int port)
    {
        Path = path;
        TargetUrl = $"http://localhost:{port}";
    }
}
