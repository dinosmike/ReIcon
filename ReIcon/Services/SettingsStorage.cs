using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReIcon.Models;

namespace ReIcon.Services;

public static class SettingsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string SettingsFileName = "ReIcon_settings.json";

    /// <summary>%LocalAppData%\ReIcon</summary>
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReIcon");

    public static string SettingsPath => Path.Combine(SettingsDirectory, SettingsFileName);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s != null)
                {
                    if (s.Meta.SettingsFormatVersion < 1)
                        s.Meta.SettingsFormatVersion = 1;
                    return s;
                }
            }
        }
        catch
        {
            // ignored
        }

        return AppSettings.CreateDefault();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // ignored
        }
    }
}
