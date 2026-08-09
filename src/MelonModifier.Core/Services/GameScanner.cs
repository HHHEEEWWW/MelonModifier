using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MelonModifier.Core.Helpers;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>
/// 游戏发现与状态检测：Steam 库扫描、Unity 引擎识别、MelonLoader 安装状态。
/// </summary>
public sealed class GameScanner
{
    private const string ProxyDllName = "version.dll";
    private const string Il2CppMarker = "GameAssembly.dll";

    /// <summary>扫描本机 Steam 库中的 Unity 游戏。</summary>
    public List<GameInfo> ScanSteamGames()
    {
        var result = new List<GameInfo>();
        var steamPath = FindSteamPath();
        if (string.IsNullOrEmpty(steamPath))
            return result;

        var steamApps = Path.Combine(steamPath, "steamapps");
        var libraryRoots = new List<string> { steamApps };
        libraryRoots.AddRange(FindExtraLibraryRoots(steamApps));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in libraryRoots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var acf in Directory.GetFiles(root, "appmanifest_*.acf"))
            {
                var (appId, name, installDir) = ReadManifest(acf);
                if (appId is null || string.IsNullOrEmpty(installDir))
                    continue;

                var gameDir = Path.Combine(root, "common", installDir);
                if (!Directory.Exists(gameDir))
                    continue;

                var engine = DetectEngine(gameDir);
                if (engine == GameEngine.Unknown)
                    continue; // 只保留 Unity 游戏

                var key = gameDir.ToLowerInvariant();
                if (!seen.Add(key))
                    continue;

                var game = new GameInfo
                {
                    Name = string.IsNullOrWhiteSpace(name) ? installDir : name,
                    Path = gameDir,
                    SteamAppId = appId,
                    Engine = engine,
                };
                DetectMelonLoader(game);
                result.Add(game);
            }
        }

        return result.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>探测一个任意目录：是否 Unity 游戏，是否装了 MelonLoader。</summary>
    public GameInfo? ProbeDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var engine = DetectEngine(directory);
        if (engine == GameEngine.Unknown)
            return null;

        var name = new DirectoryInfo(directory).Name;
        var game = new GameInfo
        {
            Name = name,
            Path = directory,
            IsManual = true,
            Engine = engine,
        };
        DetectMelonLoader(game);
        return game;
    }

    /// <summary>重新检测一个游戏的最新状态。</summary>
    public void Refresh(GameInfo game)
    {
        game.Engine = DetectEngine(game.Path);
        DetectMelonLoader(game);
    }

    /// <summary>检测 Unity 引擎类型。</summary>
    public static GameEngine DetectEngine(string gameDir)
    {
        if (!Directory.Exists(gameDir))
            return GameEngine.Unknown;

        if (File.Exists(Path.Combine(gameDir, Il2CppMarker)))
            return GameEngine.Il2Cpp;

        // Mono：任意 *_Data/Managed/Assembly-CSharp.dll
        foreach (var dataDir in Directory.GetDirectories(gameDir, "*_Data"))
        {
            if (File.Exists(Path.Combine(dataDir, "Managed", "Assembly-CSharp.dll")))
                return GameEngine.Mono;
        }

        // 有 Unity 特征但无上述标记，视为未知（可能不是标准布局）
        return GameEngine.Unknown;
    }

    /// <summary>检测 MelonLoader 是否已安装及其版本。</summary>
    public static void DetectMelonLoader(GameInfo game)
    {
        var proxy = Path.Combine(game.Path, ProxyDllName);
        var mlDir = Path.Combine(game.Path, "MelonLoader");
        game.HasMelonLoader = File.Exists(proxy) && Directory.Exists(mlDir);

        game.InstalledVersion = null;
        if (game.HasMelonLoader)
        {
            // 版本号来自 MelonLoader/net6/MelonLoader.dll 的文件版本
            foreach (var sub in new[] { "net6", "net472", "net35" })
            {
                var core = Path.Combine(mlDir, sub, "MelonLoader.dll");
                if (File.Exists(core))
                {
                    try
                    {
                        var info = FileVersionInfo.GetVersionInfo(core);
                        var v = info.FileVersion;
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            game.InstalledVersion = "v" + v;
                            break;
                        }
                    }
                    catch
                    {
                        // 忽略版本读取失败
                    }
                }
            }
        }
    }

    // ---------- Steam 内部 ----------

    private static string? FindSteamPath()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(path))
            {
                path = Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(path))
                    return path;
            }
        }
        catch
        {
            // 注册表不可用时忽略
        }
        return null;
    }

    private static IEnumerable<string> FindExtraLibraryRoots(string primarySteamApps)
    {
        var vdf = Path.Combine(primarySteamApps, "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;

        var parsed = VdfParser.Parse(File.ReadAllText(vdf));
        if (parsed is null)
            yield break;

        foreach (var kv in parsed)
        {
            if (kv.Key == "libraryfolders" && kv.Value is Dictionary<string, object> folders)
            {
                foreach (var f in folders.Values)
                {
                    if (f is Dictionary<string, object> entry
                        && entry.TryGetValue("path", out var pathObj)
                        && pathObj is string path
                        && !string.IsNullOrWhiteSpace(path))
                    {
                        yield return Path.Combine(path, "steamapps");
                    }
                }
            }
        }
    }

    private static (long? AppId, string Name, string InstallDir) ReadManifest(string acfPath)
    {
        var parsed = VdfParser.Parse(File.ReadAllText(acfPath));
        if (parsed is null || !parsed.TryGetValue("AppState", out var stateObj))
            return (null, "", "");
        if (stateObj is not Dictionary<string, object> state)
            return (null, "", "");

        state.TryGetValue("appid", out var appIdObj);
        state.TryGetValue("name", out var nameObj);
        state.TryGetValue("installdir", out var dirObj);

        long? appId = long.TryParse(appIdObj as string, out var id) ? id : null;
        return (appId, nameObj as string ?? "", dirObj as string ?? "");
    }
}
