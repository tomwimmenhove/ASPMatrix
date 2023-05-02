namespace ASPMatrix.WebServer;

public interface IWebServer
{
    Task Start();
    Task Stop();
}