using System.IO;
using System.Text.Json;
using MelonModifier.Core.Helpers;

namespace MelonModifier.Core.Services;

/// <summary>应用外观设置。</summary>
public sealed class AppSettings
{
    /// <summary>主题：Dark（夜间）/ Light（日间）。</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>界面缩放比例（0.85 ~ 1.3，等效字号调节）。</summary>
    public double FontScale { get; set; } = 1.0;

    /// <summary>主字体族名称（如 Segoe UI / Microsoft YaHei UI）。</summary>
    public string FontFamilyName { get; set; } = "Segoe UI";
}

/// <summary>外观设置读写（%AppData%/MelonModifier/settings.json）。</summary>
public sealed class SettingsService
{
    private readonly string _filePath;

    public SettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppPaths.DataDir, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    /// <summary>保存当前设置。</summary>
    public void Save() => Save(Current);

    /// <summary>保存指定设置并更新 Current。</summary>
    public void Save(AppSettings settings)
    {
        Current = settings;
        AppPaths.EnsureCreated();
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
