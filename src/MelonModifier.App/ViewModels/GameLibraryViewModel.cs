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

        // 启动即显示上次的游戏列表（缓存），随后后台自动扫描刷新
        LoadCache();
        _ = AutoScanAsync();
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

    /// <summary>启动自动扫描（失败静默，不打扰用户）。</summary>
    private async Task AutoScanAsync()
    {
        try
        {
            await Task.Delay(300); // 等 UI 先渲染出缓存列表
            await ScanCoreAsync(silent: true);
        }
        catch
        {
            // 启动扫描失败不弹窗，用户可手动点扫描
        }
    }

    [RelayCommand]
    private Task ScanAsync() => ScanCoreAsync(silent: false);

    private async Task ScanCoreAsync(bool silent)
    {
        if (IsScanning)
            return;

        IsScanning = true;
        _state.NotifyStatus("正在扫描 Steam 库 …");
        try
        {
            var found = await Task.Run(_state.Scanner.ScanSteamGames);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MergeGames(found);
                _state.Registry.SaveAll(Games);
                if (!silent)
                    _state.NotifyStatus($"扫描完成：发现 {Games.Count} 个 Unity 游戏");
            });

            await RefreshLatestAsync();
        }
        catch (Exception ex)
        {
            if (!silent)
                MessageBox.Show($"扫描失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// 合并扫描结果与当前列表：
    /// - 同一路径：保留现有条目（Id/手动标记稳定），用扫描结果刷新状态
    /// - 扫描不到：保留（手动添加或已卸载的游戏）
    /// - 新发现的：添加
    /// </summary>
    private void MergeGames(List<GameInfo> scanned)
    {
        var byPath = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in Games)
            byPath[g.Path] = g;

        var merged = new List<GameInfo>();
        foreach (var s in scanned)
        {
            if (byPath.TryGetValue(s.Path, out var existing))
            {
                // 复用现有条目，刷新状态
                existing.Engine = s.Engine;
                existing.HasMelonLoader = s.HasMelonLoader;
                existing.InstalledVersion = s.InstalledVersion;
                byPath.Remove(s.Path);
                merged.Add(existing);
            }
            else
            {
                merged.Add(s);
            }
        }

        // 扫描不到的保留（手动游戏等），并刷新状态
        foreach (var rest in byPath.Values)
        {
            _state.Scanner.Refresh(rest);
            merged.Add(rest);
        }

        merged.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        Games.Clear();
        foreach (var g in merged)
            Games.Add(g);

        foreach (var g in Games)
            g.NotifyChanged();
    }

    /// <summary>启动时从缓存加载上次的游戏列表。</summary>
    private void LoadCache()
    {
        foreach (var g in _state.Registry.Games)
            Games.Add(g);
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
        _state.Registry.SaveAll(Games);   // 缓存同步（含 Steam 扫描到的游戏）
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
            _state.Registry.SaveAll(Games);
            _state.RequestRefresh();   // 通知 Mods/日志/配置页刷新（新安装的游戏目录）
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
            _state.Registry.SaveAll(Games);
            _state.NotifyStatus($"已卸载：{game.Name}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"卸载失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task InstallBepInExAsync(GameInfo? game)
    {
        if (game is null || IsInstalling)
            return;

        var confirm = MessageBox.Show(
            $"安装 BepInEx（{game.EngineLabel} 版）到 {game.Name}？\nMono 使用稳定版 v5.4.x，Il2Cpp 使用 v6.0.0-pre 专用包。",
            "BepInEx 安装", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        IsInstalling = true;
        ShowProgress = true;
        ProgressValue = 0;
        _state.NotifyStatus($"正在安装 BepInEx → {game.Name} …");
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

            await _state.BepInExService.InstallAsync(game, progress);

            _state.NotifyStatus($"BepInEx {game.BepInExVersion} 已安装到 {game.Name}");
            ProgressText = "安装完成";
            ProgressValue = 100;

            RefreshGame(game);
            _state.Registry.SaveAll(Games);
            _state.RequestRefresh();
        }
        catch (Exception ex)
        {
            _state.NotifyStatus("BepInEx 安装失败");
            MessageBox.Show($"BepInEx 安装失败：\n{ex.Message}", "MelonModifier",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsInstalling = false;
            ShowProgress = false;
        }
    }

    [RelayCommand]
    private async Task UninstallBepInExAsync(GameInfo? game)
    {
        if (game is null || IsInstalling)
            return;

        var r = MessageBox.Show(
            $"确认卸载 {game.Name} 中的 BepInEx？\n（BepInEx/ 目录内的 Mod 会一并删除）",
            "卸载确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes)
            return;

        IsInstalling = true;
        ShowProgress = true;
        ProgressValue = 0;
        ProgressText = "正在卸载 BepInEx ...";
        try
        {
            await Task.Run(() => _state.BepInExService.UninstallAsync(game));
            RefreshGame(game);
            _state.Registry.SaveAll(Games);
            _state.NotifyStatus($"已卸载 BepInEx：{game.Name}");
            ProgressText = "卸载完成";
            ProgressValue = 100;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"卸载失败：{ex.Message}", "MelonModifier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsInstalling = false;
            ShowProgress = false;
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
