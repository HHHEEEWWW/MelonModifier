using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MelonModifier.Core.Models;
using MelonModifier.Core.Services;

namespace MelonModifier.App.ViewModels;

/// <summary>
/// 应用级共享状态：游戏列表、当前选中游戏、核心服务实例。
/// 各页面 ViewModel 通过它共享数据。
/// </summary>
public sealed partial class AppState : ObservableObject
{
    public GameScanner Scanner { get; } = new();
    public MelonLoaderService LoaderService { get; } = new();
    public ModService ModService { get; } = new();
    public LogService LogService { get; } = new();
    public ConfigService ConfigService { get; } = new();
    public GameRegistry Registry { get; } = new();

    public ObservableCollection<GameInfo> Games { get; } = new();

    [ObservableProperty]
    private GameInfo? _selectedGame;

    /// <summary>界面忙碌状态（安装/扫描时禁用操作按钮）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>底部状态栏消息。</summary>
    [ObservableProperty]
    private string _statusText = "就绪";

    /// <summary>全局刷新信号：游戏状态变化后自增，各页面据此重载数据。</summary>
    [ObservableProperty]
    private int _refreshTick;

    public void NotifyStatus(string message) => StatusText = message;

    public void RequestRefresh() => RefreshTick++;
}
