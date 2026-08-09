using System.ComponentModel;

namespace MelonModifier.Core.Models;

/// <summary>Unity 引擎类型（决定 MelonLoader 的兼容路径）。</summary>
public enum GameEngine
{
    Unknown,
    Mono,
    Il2Cpp,
}

/// <summary>一个受管的 Unity 游戏。可来自 Steam 扫描或手动添加。</summary>
public sealed class GameInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 状态字段由服务层直接赋值，修改后调用本方法通知 UI 刷新。
    /// propertyName 为 null 时表示刷新全部。
    /// </summary>
    public void NotifyChanged(string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    /// <summary>稳定标识（手动添加的游戏用于持久化）。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    /// <summary>游戏根目录（包含 version.dll / GameAssembly.dll 的目录）。</summary>
    public string Path { get; set; } = "";

    /// <summary>是否为用户手动添加（而非 Steam 扫描发现）。</summary>
    public bool IsManual { get; set; }

    /// <summary>Steam AppId（来自 Steam 扫描时）。</summary>
    public long? SteamAppId { get; set; }

    // ---------- 以下为检测结果（每次扫描刷新，不持久化） ----------

    public GameEngine Engine { get; set; } = GameEngine.Unknown;

    /// <summary>是否已安装 MelonLoader（version.dll + MelonLoader/ 目录存在）。</summary>
    public bool HasMelonLoader { get; set; }

    /// <summary>已安装的 MelonLoader 版本（无则为 null）。</summary>
    public string? InstalledVersion { get; set; }

    /// <summary>当前最新版本（获取过最新版本信息后才有值）。</summary>
    public string? LatestVersion { get; set; }

    /// <summary>已安装版本是否落后于最新版本。</summary>
    public bool IsOutdated => HasMelonLoader && LatestVersion is not null
        && InstalledVersion is not null
        && !string.Equals(InstalledVersion.TrimStart('v'), LatestVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase);

    /// <summary>是否安装了 .NET 6 Desktop Runtime（Il2Cpp 游戏安装 MelonLoader 的前提）。</summary>
    public bool HasDotNet6 { get; set; } = true;

    /// <summary>引擎的可读名称。</summary>
    public string EngineLabel => Engine switch
    {
        GameEngine.Il2Cpp => "Il2Cpp",
        GameEngine.Mono => "Mono",
        _ => "未知",
    };

    // ---------- UI 辅助（领域状态派生） ----------

    /// <summary>主操作按钮文本：安装 / 升级 / 已是最新。</summary>
    public string InstallButtonText => !HasMelonLoader ? "安装" : IsOutdated ? "升级" : "已是最新";

    /// <summary>是否允许执行安装/升级。</summary>
    public bool CanInstallOrUpgrade => !HasMelonLoader || IsOutdated;

    /// <summary>是否允许卸载。</summary>
    public bool CanUninstall => HasMelonLoader;

    /// <summary>MelonLoader 状态徽章文本。</summary>
    public string StatusText => !HasMelonLoader ? "未安装" : IsOutdated ? "可升级" : "已安装";

    /// <summary>版本摘要（已装 / 最新）。</summary>
    public string VersionSummary => InstalledVersion is null
        ? (LatestVersion is null ? "未安装" : $"最新 {LatestVersion}")
        : (LatestVersion is null ? $"已装 {InstalledVersion}" : $"{InstalledVersion} → {LatestVersion}");

    /// <summary>状态等级：0=未安装，1=已安装，2=可升级（UI 徽章颜色用）。</summary>
    public int StatusKind => !HasMelonLoader ? 0 : IsOutdated ? 2 : 1;
}
