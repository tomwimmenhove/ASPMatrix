using Microsoft.AspNetCore;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ASPMatrix.WebServer;

public static class CoreWebHostBuilder
{
    private const string StartupField = "ASPMatrix:StartupType";

    public static IWebHost BuildLocalWebHost(string path, string name, int port) =>
        BuildWebHost(path, name, $"http://localhost:{port}");

    public static IWebHost BuildWebHost(string path, string name, params Uri[] urls) =>
        BuildWebHost(path, name, urls.Select(x => x.ToString()).ToArray());

    public static IWebHost BuildWebHost(string path, string name, params string[] urls)
    {
        var appSettingsPath = Path.Combine(path, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            throw new DllNotFoundException($"{appSettingsPath}: Not found");
        }

        var dllPath = Path.Combine(path, $"{name}.dll");
        if (!File.Exists(dllPath))
        {
            throw new DllNotFoundException($"{dllPath}: Not found");
        }

        var startUpTypeName = GetStartUpType(appSettingsPath);

        var startup = Assembly.LoadFrom(dllPath).GetType(startUpTypeName);
        if (startup == null)
        {
            throw new FormatException($"{dllPath} does not contain a \"{startUpTypeName}\" type");
        }

        var args = new string[] {
            $"--urls={string.Join(',', urls)}",
            $"--environment={name}" // Forces the use of the $"appsettings.{name}.json" file
        };

        File.Copy(appSettingsPath, $"appsettings.{name}.json", true);

        return WebHost.CreateDefaultBuilder(args).UseStartup(startup).Build();
    }

    private static string GetStartUpType(string appSettingsPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false)
            .Build();

        var startUpTypeName = configuration.GetValue<string>(StartupField);
        if (startUpTypeName == null)
        {
            throw new InvalidOperationException($"No \"{StartupField}\" setting found in {appSettingsPath}.");
        }
        
        return startUpTypeName;
    }
}
