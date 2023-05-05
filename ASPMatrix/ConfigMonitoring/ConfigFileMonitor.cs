using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ASPMatrix.ConfigMonitoring;

public class ConfigFileMonitor<T> : IDisposable//, IConfigFileMonitor<T>
{
    private const string FileFilter = "*.json";

    private bool disposed = false;
    private string _directoryPath;
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly List<ConfigFile<T>> _configFiles = new();
    private readonly Thread _runnerThread;
    private readonly ILogger<ConfigFileMonitor<T>> _logger;

    public delegate Task ConfigFileEventHandler(object sender, ConfigFileEventArgs<T> e);
    public event ConfigFileEventHandler? ConfigAdded;
    public event ConfigFileEventHandler? ConfigRemoved;

    private BlockingCollection<FileSystemEventArgs> _fileEvents = new();
    private CancellationTokenSource _cancellationTokenSource = new();

    public ConfigFileMonitor(ILogger<ConfigFileMonitor<T>> logger,
        IOptions<ConfigFileSettings> options)
    {
        _logger = logger;

        _directoryPath = options.Value.ConfigPath;

        _fileSystemWatcher = new FileSystemWatcher(_directoryPath, FileFilter);

        _fileSystemWatcher.Created += OnFileSystemEvent;
        _fileSystemWatcher.Changed += OnFileSystemEvent;
        _fileSystemWatcher.Renamed += OnFileSystemEvent;
        _fileSystemWatcher.Deleted += OnFileSystemEvent;

        _runnerThread = new Thread(async () => await Run());
    }

    public void Start()
    {
        _fileSystemWatcher.EnableRaisingEvents = true;

        _runnerThread.Start();
    }

    private async Task Run()
    {
        var filePaths = Directory.GetFiles(_directoryPath, FileFilter)
            .Select(x => Path.GetFullPath(x));
        foreach (var filePath in filePaths)
        {
            ConfigFile<T> configFile;
            try
            {
                configFile = new ConfigFile<T>(filePath);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to initlaize config file \"{filePath}\": {e.Message}");
                continue;
            }
            _configFiles.Add(configFile);

            await Added(configFile);
        }

        while(!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                await WaitForChanges();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnFileSystemEvent(object? sender, FileSystemEventArgs eventArgs)
    {
        _fileEvents.Add(eventArgs);
    }

    private async Task WaitForChanges()
    {
        var fileSystemEvent = _fileEvents.Take(_cancellationTokenSource.Token);
        if (fileSystemEvent?.Name == null)
        {
            return;
        }

        var fullPath = GetFullPath(fileSystemEvent.Name);

        if (fileSystemEvent.ChangeType.HasFlag(WatcherChangeTypes.Changed))
        {
            await OnConfigChanged(fullPath);
        }
        else if (fileSystemEvent.ChangeType.HasFlag(WatcherChangeTypes.Created))
        {
            await OnConfigCreated(fullPath);
        }
        else if (fileSystemEvent.ChangeType.HasFlag(WatcherChangeTypes.Deleted))
        {
            await OnConfigDeleted(fullPath);
        }
        else if (fileSystemEvent.ChangeType.HasFlag(WatcherChangeTypes.Renamed))
        {
            if (fileSystemEvent is RenamedEventArgs renameEventArgs && renameEventArgs.OldName != null)
            {
                var oldFullPath = GetFullPath(renameEventArgs.OldName);
                OnConfigRenamed(oldFullPath, fullPath);
            }
        }
    }

    private async Task OnConfigCreated(string filePath)
    {
        _logger.LogInformation("Configuration has changed");

        ConfigFile<T> configFile;
        try
        {
            configFile = new ConfigFile<T>(filePath);
        }
        catch (Exception e)
        {
            _logger.LogError($"Failed to add config file \"{filePath}\": {e.Message}");
            return;
        }

        _configFiles.Add(configFile);
        await Added(configFile);
    }

    private async Task OnConfigChanged(string filePath)
    {
        _logger.LogInformation("Configuration has changed");
        var configFile = _configFiles.FirstOrDefault(x => x.FilePath == filePath);
        if (configFile != null)
        {
            await Removed(configFile);
            await Added(configFile);
        }
    }

    private void OnConfigRenamed(string oldPath, string newPath)
    {
        _logger.LogInformation($"Config file \"{oldPath}\" was renamed to \"{newPath}\"");
        var configFile = _configFiles.FirstOrDefault(x => x.FilePath == oldPath);
        if (configFile != null)
        {
            // TODO: Add check if it's still in the correct directory
            _configFiles.Remove(configFile);
            ConfigFile<T> newConfigFile;
            try
            {
                newConfigFile = new ConfigFile<T>(newPath);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to reload config file \"{newPath}\": {e.Message}");
                return;
            }

            _configFiles.Add(newConfigFile);
        }
    }

    private async Task OnConfigDeleted(string filePath)
    {
        _logger.LogInformation("Configuration has changed");
        var configFile = _configFiles.FirstOrDefault(x => x.FilePath == filePath);
        if (configFile != null)
        {
            _configFiles.Remove(configFile);
            await Removed(configFile);
        }
    }

    private string GetFullPath(string filePath) => Path.GetFullPath(Path.Combine(_directoryPath, filePath));

    private async Task Added(ConfigFile<T> configFile)
    {
        if (ConfigAdded != null)
        {
            await ConfigAdded(this, new ConfigFileEventArgs<T>(configFile));
        }
    }

    private async Task Removed(ConfigFile<T> configFile)
    {
        if (ConfigRemoved != null)
        {
            await ConfigRemoved(this, new ConfigFileEventArgs<T>(configFile));
        }
    }

    private T? ReadConfigFile(string filePath)
    {
        try
        {
            var jsonData = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<T>(jsonData);
            if (config == null)
            {
                _logger.LogError($"Failed to deserialize {filePath}");
                return default;
            }

            return config;
        }
        catch (Exception e)
        {
            _logger.LogError($"Failed to deserialize {filePath}: {e.Message}");
            return default;
        }
    }

    ~ConfigFileMonitor()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                if (_runnerThread.ThreadState == ThreadState.Running)
                {
                    _runnerThread.Join();
                }
            }

            disposed = true;
        }
    }

    // private void GetRemovedConfigs(IList<ConfigFile<T>> newConfigFiles)
    // {
    //     for (var i = _confileFiles.Count - 1; i >= 0; i--)
    //     {
    //         var configFile = _confileFiles[i];

    //         var newConfigFile = newConfigFiles.FirstOrDefault(x => x.FilePath == configFile.FilePath);
    //         if (newConfigFile == null || !configFile.Equals(newConfigFile))
    //         {
    //             ConfigRemoved?.Invoke(this, new ConfigFileEventArgs<T>(configFile));
    //             _confileFiles.RemoveAt(i);
    //         }
    //     }

    //     var addConfigFiles = newConfigFiles.Where(x => _confileFiles.All(y => y.FilePath != x.FilePath));
    //     foreach(var configFile in addConfigFiles)
    //     {
    //         ConfigAdded?.Invoke(this, new ConfigFileEventArgs<T>(configFile));
    //         _confileFiles.Add(configFile);
    //     }
    // }
}
