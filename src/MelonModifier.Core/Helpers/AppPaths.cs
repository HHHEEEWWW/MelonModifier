using System.IO;

namespace MelonModifier.Core.Helpers;

/// <summary>应用数据目录等固定路径。</summary>
public static class AppPaths
{
    /// <summary>%AppData%/MelonModifier —— 存放用户数据。</summary>
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MelonModifier");

    /// <summary>用户手动添加的游戏列表。</summary>
    public static string GamesJson => Path.Combine(DataDir, "games.json");

    /// <summary>下载缓存目录。</summary>
    public static string CacheDir => Path.Combine(DataDir, "cache");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(CacheDir);
    }
}
