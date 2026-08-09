using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MelonModifier.Core.Services;

namespace MelonModifier.App.ViewModels;

/// <summary>配置页：查看 / 编辑游戏的 UserData/Loader.cfg（全文模式，保留注释与未知键）。</summary>
public sealed partial class ConfigViewModel : ObservableObject
{
    private readonly AppState _state;

    public ConfigViewModel(AppState state)
    {
        _state = state;

        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedGame))
                Reload();
            else if (e.PropertyName == nameof(AppState.RefreshTick))
                Reload();
        };
    }

    public ObservableCollection<ConfigEntry> Entries { get; } = new();

    [ObservableProperty]
    private bool _hasGame;

    [ObservableProperty]
    private string _gameHint = "请在游戏库中选择一个游戏";

    [ObservableProperty]
    private string _rawContent = "";

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _configPath = "";

    [RelayCommand]
    private void ReloadConfig() => Reload();

    [RelayCommand]
    private void SaveConfig()
    {
        var game = _state.SelectedGame;
        if (game is null)
            return;

        try
        {
            _state.ConfigService.Write(game, RawContent);
            IsDirty = false;
            _state.NotifyStatus("Loader.cfg 已保存");
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenConfigFile()
    {
        if (!File.Exists(ConfigPath))
            return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConfigPath)
        {
            UseShellExecute = true,
        });
    }

    private void Reload()
    {
        var game = _state.SelectedGame;
        HasGame = game is not null;
        GameHint = game is null ? "请在游戏库中选择一个游戏" : $"配置：{game.Name}";

        Entries.Clear();
        RawContent = "";
        ConfigPath = "";
        if (game is null)
            return;

        var path = _state.ConfigService.GetConfigPath(game);
        if (path is null)
        {
            GameHint = $"尚未生成配置：{game.Name}（安装 MelonLoader 并启动游戏后自动创建）";
            return;
        }

        ConfigPath = path;
        var read = _state.ConfigService.Read(game);
        if (read is null)
            return;

        foreach (var entry in read.Value.Entries)
            Entries.Add(entry);

        RawContent = read.Value.Raw;
        IsDirty = false;
    }

    partial void OnRawContentChanged(string value) => IsDirty = true;
}
