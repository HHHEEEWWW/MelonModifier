using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MelonModifier.Core.Models;

namespace MelonModifier.App.ViewModels;

/// <summary>Mods 页：浏览 / 启停 / 安装 / 删除游戏的 Mods 与 Plugins。</summary>
public sealed partial class ModsViewModel : ObservableObject
{
    private readonly AppState _state;

    public ModsViewModel(AppState state)
    {
        _state = state;
        Games = state.Games;

        // 选中游戏变化或全局刷新信号 → 重载列表
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedGame))
                Reload();
            else if (e.PropertyName == nameof(AppState.RefreshTick))
                Reload();
        };
    }

    public ObservableCollection<GameInfo> Games { get; }

    public ObservableCollection<ModInfo> Mods { get; } = new();
    public ObservableCollection<ModInfo> Plugins { get; } = new();

    private static string ModsDir(GameInfo game) => Path.Combine(game.Path, "Mods");
    private static string PluginsDir(GameInfo game) => Path.Combine(game.Path, "Plugins");

    [ObservableProperty]
    private bool _hasGame;

    [ObservableProperty]
    private string _gameHint = "请在游戏库中选择一个游戏";

    public bool HasMods => Mods.Count > 0;
    public bool HasPlugins => Plugins.Count > 0;

    private void Reload()
    {
        var game = _state.SelectedGame;
        HasGame = game is not null;
        GameHint = game is null ? "请在游戏库中选择一个游戏" : $"管理：{game.Name} 的本地模组";

        Mods.Clear();
        Plugins.Clear();
        if (game is null)
            return;

        foreach (var m in _state.ModService.ListMods(game))
        {
            if (m.IsPlugin) Plugins.Add(m);
            else Mods.Add(m);
        }

        OnPropertyChanged(nameof(HasMods));
        OnPropertyChanged(nameof(HasPlugins));
    }

    [RelayCommand]
    private void RefreshList() => Reload();

    /// <summary>启停切换（ModInfo 上已双向绑定 IsEnabled）。</summary>
    [RelayCommand]
    private void ToggleMod(ModInfo? mod)
    {
        if (mod is null)
            return;
        try
        {
            _state.ModService.SetEnabled(mod, mod.IsEnabled);
            _state.NotifyStatus($"{(mod.IsEnabled ? "已启用" : "已停用")} {mod.Name}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"切换失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
            Reload();
        }
    }

    [RelayCommand]
    private void DeleteMod(ModInfo? mod)
    {
        if (mod is null)
            return;
        var r = MessageBox.Show($"删除 {mod.Name}？（文件将被移除）", "删除确认",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes)
            return;

        try
        {
            _state.ModService.Delete(mod);
            _state.NotifyStatus($"已删除 {mod.Name}");
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenModsFolder()
    {
        var game = _state.SelectedGame;
        if (game is null)
            return;
        var dir = ModsDir(game);
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenPluginsFolder()
    {
        var game = _state.SelectedGame;
        if (game is null)
            return;
        var dir = PluginsDir(game);
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
    }

    /// <summary>拖拽安装一个 DLL（code-behind 调用）。</summary>
    public void InstallDroppedFile(string sourcePath, bool isPlugin)
    {
        var game = _state.SelectedGame;
        if (game is null)
            return;

        try
        {
            var mod = _state.ModService.InstallDll(game, sourcePath, isPlugin);
            if (mod is not null)
            {
                _state.NotifyStatus($"已安装 {mod.Name} → {(isPlugin ? "Plugins" : "Mods")}");
                Reload();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"安装失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
