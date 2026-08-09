using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MelonModifier.App.ViewModels;

/// <summary>字体选项。</summary>
public sealed record FontOption(string Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>外观页：主题（夜间/日间）、字体族、字号缩放。</summary>
public sealed partial class AppearanceViewModel : ObservableObject
{
    private readonly AppState _state;

    public AppearanceViewModel(AppState state)
    {
        _state = state;

        var settings = state.SettingsService.Current;

        IsDark = settings.Theme != "Light";
        SelectedFontFamily = FontFamilies.Find(f => f.Value == settings.FontFamilyName) ?? FontFamilies[0];
        FontScale = settings.FontScale;
    }

    public List<FontOption> FontFamilies { get; } = new()
    {
        new("Segoe UI", "Segoe UI（系统默认）"),
        new("Microsoft YaHei UI", "微软雅黑"),
        new("SimSun", "宋体"),
        new("SimHei", "黑体"),
    };

    /// <summary>是否夜间主题。</summary>
    [ObservableProperty]
    private bool _isDark = true;

    [ObservableProperty]
    private FontOption? _selectedFontFamily;

    [ObservableProperty]
    private double _fontScale = 1.0;

    partial void OnIsDarkChanged(bool value)
    {
        var theme = value ? "Dark" : "Light";
        App.ApplyTheme(theme);
        _state.SettingsService.Current.Theme = theme;
        _state.SettingsService.Save();
        _state.NotifyStatus($"主题已切换为 {theme switch { "Dark" => "夜间（暗色）", _ => "日间（亮色）" }}");
    }

    partial void OnSelectedFontFamilyChanged(FontOption? value)
    {
        if (value is null)
            return;
        App.ApplyFont(value.Value);
        _state.SettingsService.Current.FontFamilyName = value.Value;
        _state.SettingsService.Save();
        _state.NotifyStatus($"字体已切换为 {value.Label}");
    }

    partial void OnFontScaleChanged(double value)
    {
        // 拖动过程中实时保存（设置体积小，无性能问题）
        _state.SettingsService.Current.FontScale = value;
        _state.SettingsService.Save();
    }
}
