using System.Text.Json.Serialization;
using ReIcon.Services;

namespace ReIcon.Models;

public sealed class AppSettings
{
    [JsonPropertyName("ui")]
    public UiAppearanceSettings Ui { get; set; } = new();

    [JsonPropertyName("meta")]
    public SettingsMeta Meta { get; set; } = new();

    [JsonIgnore]
    public string? UiLanguage
    {
        get => Ui.Language;
        set => Ui.Language = value;
    }

    /// <summary>Галочка «Проверять обновления» (модуль ReUpdate). Включена по умолчанию.</summary>
    [JsonIgnore]
    public bool AutoUpdate
    {
        get => Ui.AutoUpdate;
        set => Ui.AutoUpdate = value;
    }

    /// <summary>Новый файл настроек: язык не задан — берётся из системы до первого выбора в UI.</summary>
    public static AppSettings CreateDefault() => new()
    {
        Ui = new UiAppearanceSettings { Language = null },
        Meta = new SettingsMeta { SettingsFormatVersion = 1 }
    };
}

public sealed class UiAppearanceSettings
{
    public string? Language { get; set; }

    public bool AutoUpdate { get; set; } = true;
}

public sealed class SettingsMeta
{
    public int SettingsFormatVersion { get; set; } = 1;
}
