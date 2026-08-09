using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MelonModifier.Core.Models;
using MelonModifier.Core.Services;

namespace MelonModifier.App.ViewModels;

/// <summary>游戏库页：扫描 / 添加 / 安装 / 升级 / 卸载 MelonLoader。</summary>
public sealed partial class GameLibraryViewModel : ObservableObject
{
    private readonly AppState _state;

    public GameLibraryViewModel(AppState state)
    {
        _state = state;
        Games = state.Games;
    }

    public ObservableCollection<GameInfo> Games { get; }

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _showProgress;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private GameInfo? _selectedGame;

    /// <summary>所选游戏详情面板可见。</summary>
    public bool HasSelection => SelectedGame is not null;

    partial void OnSelectedGameChanged(GameInfo? value)
    {
        _state.SelectedGame = value;
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning)
            return;

        IsScanning = true;
        _state.NotifyStatus("正在扫描 Steam 库 …");
        try
        {
            var found = await Task.Run(_state.Scanner.ScanSteamGames);

            // 合并手动添加的游戏
            foreach (var manual in _state.Registry.Games)
            {
                var existing = found.Find(g => string.Equals(g.Path, manual.Path, System.StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    _state.Scanner.Refresh(manual);
                    found.Add(manual);
                }
            }

            Games.Clear();
            foreach (var g in found)
                Games.Add(g);

            _state.NotifyStatus($"扫描完成：发现 {Games.Count} 个 Unity 游戏");

            // 后台查询最新版本
            _ = Task.Run(async () =>
            {
                var release = await _state.LoaderService.GetLatestReleaseAsync();
                if (release is null)
                    return;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var g in Games)
                    {
                        g.LatestVersion = release.VersionLabel;
                        g.NotifyChanged();
                    }
                });
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"扫描失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void AddGame()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择 Unity 游戏目录（包含 GameAssembly.dll 或 *_Data 的文件夹）",
        };
        if (dialog.ShowDialog() != true)
            return;

        var game = _state.Scanner.ProbeDirectory(dialog.FolderName);
        if (game is null)
        {
            MessageBox.Show("该目录不是可识别的 Unity 游戏（未找到 GameAssembly.dll 或 *_Data/Managed）。",
                "无法添加", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _state.Registry.Add(game);
        if (Games.All(g => !string.Equals(g.Path, game.Path, System.StringComparison.OrdinalIgnoreCase)))
            Games.Add(game);
        _state.NotifyStatus($"已添加：{game.Name}");
    }

    [RelayCommand]
    private void SelectGame(GameInfo? game) => SelectedGame = game;

    [RelayCommand]
    private async Task InstallAsync(GameInfo? game)
    {
        if (game is null || IsInstalling)
            return;

        var confirm = game.HasMelonLoader
            ? MessageBox.Show($"将 MelonLoader 升级到最新版本？\n{game.Name}", "升级确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBoxResult.Yes;
        if (confirm != MessageBoxResult.Yes)
            return;

        if (game.Engine == GameEngine.Il2Cpp && !MelonLoaderService.HasDotNet6Runtime())
        {
            var r = MessageBox.Show("该游戏为 Il2Cpp 引擎，需要 .NET 6 Desktop Runtime。\n是否打开下载页面？",
                "缺少运行时", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://dotnet.microsoft.com/en-us/download/dotnet/6.0") { UseShellExecute = true });
            return;
        }

        IsInstalling = true;
        ShowProgress = true;
        ProgressValue = 0;
        _state.NotifyStatus($"正在安装 MelonLoader → {game.Name} …");
        try
        {
            var progress = new Progress<string>(s =>
            {
                ProgressText = s;
                if (s.Contains('%'))
                {
                    var idx = s.LastIndexOf('%');
                    if (double.TryParse(s.AsSpan(0, idx).Trim(), out var pct))
                        ProgressValue = pct;
                }
            });

            await _state.LoaderService.InstallAsync(game, progress);

            _state.NotifyStatus($"MelonLoader {game.InstalledVersion} 已安装到 {game.Name}");
            ProgressText = "安装完成";
            ProgressValue = 100;

            RefreshGame(game);
            await RefreshLatestAsync();
        }
        catch (Exception ex)
        {
            _state.NotifyStatus("安装失败");
            MessageBox.Show($"安装失败：\n{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsInstalling = false;
            ShowProgress = false;
        }
    }

    [RelayCommand]
    private void Uninstall(GameInfo? game)
    {
        if (game is null)
            return;

        var r = MessageBox.Show(
            $"确认卸载 {game.Name} 中的 MelonLoader？\n（Mods、Plugins、UserData 会保留）",
            "卸载确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes)
            return;

        try
        {
            _state.LoaderService.UninstallAsync(game).GetAwaiter().GetResult();
            RefreshGame(game);
            _state.NotifyStatus($"已卸载：{game.Name}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"卸载失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenGameFolder(GameInfo? game)
    {
        if (game is null || !Directory.Exists(game.Path))
            return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(game.Path)
        {
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private void RefreshAll()
    {
        foreach (var g in Games)
            RefreshGame(g);
        _state.NotifyStatus($"已刷新 {Games.Count} 个游戏的状态");
        _ = RefreshLatestAsync();
    }

    private void RefreshGame(GameInfo game)
    {
        _state.Scanner.Refresh(game);
        game.NotifyChanged();
    }

    private async Task RefreshLatestAsync()
    {
        var release = await _state.LoaderService.GetLatestReleaseAsync();
        if (release is null)
            return;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var g in Games)
            {
                g.LatestVersion = release.VersionLabel;
                g.NotifyChanged();
            }
        });
    }
}
