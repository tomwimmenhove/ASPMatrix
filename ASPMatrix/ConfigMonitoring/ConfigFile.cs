using System;
using System.Text.Json;

namespace ASPMatrix.ConfigMonitoring;

public class ConfigFile<T>
{
    public string FilePath { get; set; }
    public T Config { get; set; }

    public ConfigFile(string filePath)
    {
        FilePath = filePath;
        if (File.Exists(FilePath))
        {
            var contents = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<T>(contents);
            if (config == null)
            {
                throw new JsonException($"Failed to deserialize {filePath}");
            }

            Config = config;
        }
        else
        {
            throw new FileNotFoundException($"Could not find {filePath}");
        }
    }
}
