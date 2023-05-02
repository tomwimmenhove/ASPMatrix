namespace ASPMatrix.Proxy;

public interface IProxyConfigurator
{
    Task SetProxyRules(IEnumerable<ProxyRule> rules);
}
