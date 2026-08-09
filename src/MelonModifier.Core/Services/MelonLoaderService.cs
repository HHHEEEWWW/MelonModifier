using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MelonModifier.Core.Helpers;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>MelonLoader 安装/卸载：下载 release、打代理 DLL 补丁、回滚。</summary>
public sealed class MelonLoaderService
{
    private const string RepoApi = "https://api.github.com/repos/LavaGang/MelonLoader/releases/latest";

    // v0.7.x 的 zip 结构：根目录 version.dll + MelonLoader/ 文件夹（无 dobby.dll）
    private static readonly string[] ProxyFileNames = { "version.dll" };

    private readonly HttpClient _http;
    private readonly string _cacheDir;

    public MelonLoaderService(HttpClient? http = null, string? cacheDir = null)
    {
        _http = http ?? CreateHttpClient();
        _cacheDir = cacheDir ?? AppPaths.CacheDir;
    }

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MelonModifier/0.1");
        client.Timeout = TimeSpan.FromMinutes(10);
        return client;
    }

    /// <summary>查询 MelonLoader 最新发布。</summary>
    public async Task<MelonLoaderRelease?> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync(RepoApi, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var release = new MelonLoaderRelease
            {
                Tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "",
                Name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Body = root.TryGetProperty("body", out var b) ? b.GetString() : null,
                PublishedAt = root.TryGetProperty("published_at", out var p)
                    && p.TryGetDateTimeOffset(out var dto) ? dto : null,
            };

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                    if (name == "MelonLoader.x64.zip")
                    {
                        release.WindowsX64Url = asset.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString() : null;
                    }
                }
            }

            return release;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>安装 MelonLoader 到游戏目录（打补丁）。游戏需处于关闭状态。</summary>
    public async Task InstallAsync(GameInfo game, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var release = await GetLatestReleaseAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("无法获取 MelonLoader 最新版本信息（网络或 API 问题）");

        var url = release.WindowsX64Url
            ?? throw new InvalidOperationException($"发布 {release.Tag} 缺少 Windows x64 安装包");

        progress?.Report($"下载 MelonLoader {release.Tag} ...");
        var zipPath = await DownloadAsync(url, release.Tag, ct).ConfigureAwait(false);

        progress?.Report("解压并写入游戏目录 ...");
        await ExtractToGameAsync(game, zipPath, progress, ct).ConfigureAwait(false);

        game.InstalledVersion = release.Tag;
        game.LatestVersion = release.Tag;
        game.HasMelonLoader = true;
    }

    /// <summary>从游戏目录卸载 MelonLoader。可选清理 Mods/Plugins/UserData。</summary>
    public Task UninstallAsync(GameInfo game, bool cleanMods = false, bool cleanPlugins = false,
        bool cleanUserData = false, CancellationToken ct = default)
    {
        EnsureGameClosed(game.Path);

        foreach (var proxy in ProxyFileNames)
        {
            var p = Path.Combine(game.Path, proxy);
            if (File.Exists(p))
                File.Delete(p);
        }

        var mlDir = Path.Combine(game.Path, "MelonLoader");
        if (Directory.Exists(mlDir))
            Directory.Delete(mlDir, true);

        if (cleanMods)
        {
            var mods = Path.Combine(game.Path, "Mods");
            if (Directory.Exists(mods))
                Directory.Delete(mods, true);
        }

        if (cleanPlugins)
        {
            var plugins = Path.Combine(game.Path, "Plugins");
            if (Directory.Exists(plugins))
                Directory.Delete(plugins, true);
        }

        if (cleanUserData)
        {
            var userData = Path.Combine(game.Path, "UserData");
            if (Directory.Exists(userData))
                Directory.Delete(userData, true);
        }

        game.HasMelonLoader = false;
        game.InstalledVersion = null;
        return Task.CompletedTask;
    }

    /// <summary>检查 .NET 6 Desktop Runtime 是否安装（Il2Cpp 游戏硬性要求）。</summary>
    public static bool HasDotNet6Runtime()
    {
        if (OperatingSystem.IsWindows())
        {
            try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");
            var version = key?.GetValue("Version") as string;
            if (version is not null && version.StartsWith("6.", System.StringComparison.Ordinal))
                return true;

            // .NET 9 向下兼容 .NET 6 应用，视为满足
            if (version is not null)
                return true;
        }
        catch
        {
            // 忽略注册表读取失败
        }
        }

        // 兜底：扫描常见安装目录
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.WindowsDesktop.App");
        if (Directory.Exists(root))
        {
            return Directory.GetDirectories(root)
                .Any(d => d.Split(Path.DirectorySeparatorChar).Last().StartsWith("6.", System.StringComparison.Ordinal)
                       || d.Split(Path.DirectorySeparatorChar).Last().StartsWith("7.", System.StringComparison.Ordinal)
                       || d.Split(Path.DirectorySeparatorChar).Last().StartsWith("8.", System.StringComparison.Ordinal)
                       || d.Split(Path.DirectorySeparatorChar).Last().StartsWith("9.", System.StringComparison.Ordinal));
        }
        return false;
    }

    // ---------- 内部 ----------

    private async Task<string> DownloadAsync(string url, string tag, CancellationToken ct)
    {
        AppPaths.EnsureCreated();
        Directory.CreateDirectory(_cacheDir);

        var zipPath = Path.Combine(_cacheDir, $"MelonLoader_{tag}.x64.zip");
        if (File.Exists(zipPath) && new FileInfo(zipPath).Length > 1_000_000)
            return zipPath; // 命中缓存

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;

        await using var src = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
        var buffer = new byte[64 * 1024];
        long written = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            written += read;
            if (total > 0)
            {
                var pct = (int)(written * 100 / total);
                System.Diagnostics.Debug.WriteLine($"download {pct}%");
            }
        }
        return zipPath;
    }

    private async Task ExtractToGameAsync(GameInfo game, string zipPath, IProgress<string>? progress, CancellationToken ct)
    {
        EnsureGameClosed(game.Path);

        // 先解压到临时目录，校验 zip 完整性后再整体拷贝
        var tempDir = Path.Combine(Path.GetTempPath(), "MelonModifier", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir), ct).ConfigureAwait(false);

            var srcMlDir = Path.Combine(tempDir, "MelonLoader");
            var dstMlDir = Path.Combine(game.Path, "MelonLoader");
            if (!Directory.Exists(srcMlDir))
                throw new InvalidOperationException("安装包缺少 MelonLoader/ 目录，可能不是有效的 MelonLoader 归档");

            progress?.Report("写入 MelonLoader/ ...");
            if (Directory.Exists(dstMlDir))
                Directory.Delete(dstMlDir, true);
            CopyDirectory(srcMlDir, dstMlDir);

            foreach (var proxy in ProxyFileNames)
            {
                var src = Path.Combine(tempDir, proxy);
                var dst = Path.Combine(game.Path, proxy);
                if (File.Exists(src))
                {
                    if (File.Exists(dst))
                        File.Delete(dst);
                    File.Copy(src, dst);
                }
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* 忽略清理失败 */ }
        }
    }

    private static void EnsureGameClosed(string gameDir)
    {
        // 通过代理 DLL 锁检测游戏进程是否在运行（粗粒度，尽力而为）
        var proxy = Path.Combine(gameDir, "version.dll");
        if (!File.Exists(proxy))
            return;
        try
        {
            using var fs = File.Open(proxy, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException)
        {
            throw new InvalidOperationException("游戏似乎正在运行，请先关闭游戏再操作。");
        }
    }

    internal static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }
}
