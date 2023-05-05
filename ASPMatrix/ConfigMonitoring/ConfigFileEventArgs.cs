namespace ASPMatrix.ConfigMonitoring;

public class ConfigFileEventArgs<T> : EventArgs
{
    public ConfigFile<T> ConfigFile { get; }

    public ConfigFileEventArgs(ConfigFile<T> configFile)
    {
        ConfigFile = configFile;
    }
}
