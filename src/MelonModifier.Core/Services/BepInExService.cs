using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MelonModifier.Core.Helpers;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>
/// BepInEx 安装/卸载：按引擎选择发布包（Mono 用稳定版 v5.4.x，Il2Cpp 用 v6.0.0-pre 的 IL2CPP 包），
/// 解压到游戏根目录（winhttp.dll 代理 + doorstop_config.ini + BepInEx/）。
/// </summary>
public sealed class BepInExService
{
    private const string RepoApi = "https://api.github.com/repos/BepInEx/BepInEx";

    // Il2Cpp 只有 pre-release 的专用包（BepInEx 6）；Mono 用稳定版 v5.4.23.5。
    private const string Il2CppPreTag = "v6.0.0-pre.2";
    private const string Il2CppAssetPrefix = "BepInEx-Unity.IL2CPP-win-x64-";

    // API 限流（匿名 60 次/小时）时的固定版本回退：与 GetLatestReleaseAsync 的动态结果一致。
    private const string MonoFallbackUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip";
    private const string Il2CppFallbackUrl = "https://github.com/BepInEx/BepInEx/releases/download/v6.0.0-pre.2/BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip";

    // 代理 DLL 与 Doorstop 配置文件（BepInEx 的安装标记）
    private static readonly string[] ProxyFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" };

    // 部署标记：记录安装时额外部署到游戏根目录的目录（如 Il2Cpp 包的 dotnet/），卸载时按标记精确清理
    private const string DeployMarkerFile = "BepInEx/.melonmodifier_deployed";

    private readonly HttpClient _http;
    private readonly string _cacheDir;

    public BepInExService(HttpClient? http = null, string? cacheDir = null)
    {
        _http = http ?? HttpClientFactory.Create();
        _cacheDir = cacheDir ?? AppPaths.CacheDir;
    }

    /// <summary>查询适合指定引擎的 BepInEx 发布信息。Mono→稳定版；Il2Cpp→pre-release 专用包。</summary>
    public async Task<(string Tag, string ZipUrl)?> GetLatestReleaseAsync(GameEngine engine, CancellationToken ct = default)
    {
        try
        {
            if (engine == GameEngine.Il2Cpp)
            {
                // Il2Cpp 专用包只在 pre-release（v6.0.0-pre.2）提供
                var json = await _http.GetStringAsync($"{RepoApi}/releases/tags/{Il2CppPreTag}", ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.StartsWith(Il2CppAssetPrefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                        if (url != null)
                            return (Il2CppPreTag, url);
                    }
                }
                return null;
            }

            // Mono：稳定版 latest
            var latest = await _http.GetStringAsync($"{RepoApi}/releases/latest", ct).ConfigureAwait(false);
            using var doc2 = JsonDocument.Parse(latest);
            var root = doc2.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                if (name.StartsWith("BepInEx_win_x64_", System.StringComparison.OrdinalIgnoreCase))
                {
                    var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (url != null)
                        return (tag, url);
                }
            }
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // API 不可用（限流/网络）时回退到固定已知版本，保证安装功能可用
            return engine == GameEngine.Il2Cpp
                ? (Il2CppPreTag, Il2CppFallbackUrl)
                : ("v5.4.23.5", MonoFallbackUrl);
        }
    }

    /// <summary>安装 BepInEx 到游戏目录（解压整包到根目录）。游戏需处于关闭状态。</summary>
    public async Task InstallAsync(GameInfo game, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var rel = await GetLatestReleaseAsync(game.Engine, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("无法获取 BepInEx 发布信息（网络或 API 问题）");

        progress?.Report($"下载 BepInEx {rel.Tag}（{game.EngineLabel} 版）...");
        var zipPath = await DownloadAsync(rel.ZipUrl, rel.Tag, game.Engine, progress, ct).ConfigureAwait(false);

        progress?.Report("解压并写入游戏目录 ...");
        await ExtractToGameAsync(game, zipPath, ct).ConfigureAwait(false);

        game.HasBepInEx = true;
        game.BepInExVersion = DetectVersion(game.Path);
    }

    /// <summary>从游戏目录卸载 BepInEx（删代理 DLL + Doorstop 配置 + BepInEx/ 目录 + 按清单删除部署的额外文件）。</summary>
    public Task UninstallAsync(GameInfo game, CancellationToken ct = default)
    {
        EnsureGameClosed(game.Path);

        // 先按清单删除额外部署的文件（Il2Cpp 包的 dotnet/ BCL），保留游戏自有文件；
        // 单文件失败（占用/损坏）不中断整体卸载，降级为跳过
        var marker = Path.Combine(game.Path, DeployMarkerFile);
        if (File.Exists(marker))
        {
            var lines = File.ReadAllLines(marker);
            foreach (var line in lines)
            {
                var rel = line.Trim();
                // 路径防御：仅接受相对路径且不含 .. 穿越
                if (rel.Length == 0 || rel.StartsWith('/') || rel.StartsWith('\\')
                    || rel.Contains("..") || Path.IsPathRooted(rel))
                    continue;
                var p = Path.Combine(game.Path, rel);
                try
                {
                    if (File.Exists(p))
                        File.Delete(p);
                }
                catch (IOException) { /* 文件被占用，跳过 */ }
                catch (UnauthorizedAccessException) { /* 权限不足，跳过 */ }
            }
            // 清理清单涉及的目录（仅删除因此变空的目录，非空时忽略）
            foreach (var line in lines)
            {
                var dirRel = Path.GetDirectoryName(line);
                if (string.IsNullOrEmpty(dirRel) || dirRel.Contains("..") || Path.IsPathRooted(dirRel))
                    continue;
                try
                {
                    Directory.Delete(Path.Combine(game.Path, dirRel), recursive: false);
                }
                catch (IOException) { /* 目录非空（含游戏自有文件），保留 */ }
                catch (UnauthorizedAccessException) { /* 忽略 */ }
            }
        }

        foreach (var f in ProxyFiles)
        {
            var p = Path.Combine(game.Path, f);
            if (File.Exists(p))
                File.Delete(p);
        }

        var dir = Path.Combine(game.Path, "BepInEx");
        if (Directory.Exists(dir))
            Directory.Delete(dir, true); // 标记文件在 BepInEx/ 内，随框架一并删除

        game.HasBepInEx = false;
        game.BepInExVersion = null;
        return Task.CompletedTask;
    }

    /// <summary>检测游戏目录是否已安装 BepInEx 并返回版本（未安装返回 null）。</summary>
    public static string? DetectVersion(string gameDir)
    {
        var core = FindCoreDll(gameDir);
        if (core is null)
            return null;
        try
        {
            var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(core).FileVersion;
            return string.IsNullOrWhiteSpace(v) ? "已安装" : v;
        }
        catch
        {
            return "已安装";
        }
    }

    /// <summary>
    /// 定位 BepInEx 核心程序集：Mono 包为 BepInEx/core/BepInEx.dll，
    /// Il2Cpp 包（v6.0.0-pre）为 BepInEx/core/BepInEx.Core.dll。
    /// </summary>
    internal static string? FindCoreDll(string gameDir)
    {
        var coreDir = Path.Combine(gameDir, "BepInEx", "core");
        foreach (var name in new[] { "BepInEx.dll", "BepInEx.Core.dll" })
        {
            var p = Path.Combine(coreDir, name);
            if (File.Exists(p))
                return p;
        }
        return null;
    }

    // ---------- 内部 ----------

    private async Task<string> DownloadAsync(string url, string tag, GameEngine engine,
        IProgress<string>? progress, CancellationToken ct)
    {
        AppPaths.EnsureCreated();
        Directory.CreateDirectory(_cacheDir);

        var engineTag = engine == GameEngine.Il2Cpp ? "il2cpp" : "mono";
        var zipPath = Path.Combine(_cacheDir, $"BepInEx_{engineTag}_{tag}.zip");

        // 缓存命中：校验 zip 中央目录可读，防止半截文件被误用
        if (File.Exists(zipPath) && IsValidZip(zipPath))
            return zipPath;

        // 下载到 .part，完成后原子改名，避免中断残留
        var partPath = zipPath + ".part";
        using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;

            await using var src = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var dst = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            var buffer = new byte[64 * 1024];
            long written = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                written += read;
                if (total > 0)
                    progress?.Report($"下载 {written * 100 / total}%");
            }
        }

        // 校验下载完整性后再改名进缓存（200 响应可能是错误页/损坏包）
        if (!IsValidZip(partPath))
        {
            try { File.Delete(partPath); } catch { /* 忽略 */ }
            throw new InvalidOperationException("下载的文件不完整或损坏，请重试。");
        }
        File.Move(partPath, zipPath, overwrite: true);
        return zipPath;
    }

    private static bool IsValidZip(string path)
    {
        try
        {
            using var z = ZipFile.OpenRead(path);
            return z.Entries.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task ExtractToGameAsync(GameInfo game, string zipPath, CancellationToken ct)
    {
        EnsureGameClosed(game.Path);

        var tempDir = Path.Combine(Path.GetTempPath(), "MelonModifier", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir), ct).ConfigureAwait(false);

            // 定位包根目录：zip 根含 winhttp.dll / BepInEx/；若为嵌套目录则提升一层
            string root = FindPackageRoot(tempDir);

            // 只部署运行时目录（BepInEx/ + 可选的 dotnet/，Il2Cpp 包需要）与代理文件，
            // 不整包拷贝，避免覆盖游戏目录文件。
            var deployedAny = false;
            var deployedFiles = new List<string>();
            foreach (var sub in new[] { "BepInEx", "dotnet" })
            {
                var srcDir = Path.Combine(root, sub);
                if (!Directory.Exists(srcDir))
                    continue;
                var dstDir = Path.Combine(game.Path, sub);
                if (sub == "BepInEx")
                {
                    // 框架目录整体替换（保留用户数据目录：plugins/config，避免重装丢 Mod）
                    var keepDirs = new List<(string Name, string Temp)>();
                    if (Directory.Exists(dstDir))
                    {
                        foreach (var keep in new[] { "plugins", "config" })
                        {
                            var keepSrc = Path.Combine(dstDir, keep);
                            if (Directory.Exists(keepSrc))
                            {
                                var tempKeep = Path.Combine(Path.GetTempPath(), "MelonModifier",
                                    Guid.NewGuid().ToString("N"));
                                Directory.Move(keepSrc, tempKeep);
                                keepDirs.Add((keep, tempKeep));
                            }
                        }
                        Directory.Delete(dstDir, true);
                    }
                    CopyDirectory(srcDir, dstDir);
                    // 恢复用户数据目录（合并进新框架目录）
                    foreach (var (name, temp) in keepDirs)
                    {
                        CopyDirectory(temp, Path.Combine(dstDir, name));
                        try { Directory.Delete(temp, true); } catch { /* 忽略 */ }
                    }
                }
                else
                {
                    // 额外目录（dotnet/）合并拷贝：覆盖同名文件但保留游戏自有文件
                    CopyDirectory(srcDir, dstDir);
                    foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                        deployedFiles.Add(rel);
                    }
                }
                deployedAny = true;
            }
            if (!deployedAny)
                throw new InvalidOperationException("安装包缺少 BepInEx/ 目录，可能不是有效的 BepInEx 归档");

            foreach (var f in ProxyFiles)
            {
                var src = Path.Combine(root, f);
                var dst = Path.Combine(game.Path, f);
                if (File.Exists(src))
                {
                    if (File.Exists(dst))
                        File.Delete(dst);
                    File.Copy(src, dst);
                }
            }

            // 写部署清单（供卸载精确清理额外部署的文件）
            if (deployedFiles.Count > 0)
                File.WriteAllLines(Path.Combine(game.Path, DeployMarkerFile), deployedFiles);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* 忽略清理失败 */ }
        }
    }

    /// <summary>定位包根目录：zip 根含 winhttp.dll 或 BepInEx/ 则为根；否则取唯一子目录。</summary>
    private static string FindPackageRoot(string dir)
    {
        if (File.Exists(Path.Combine(dir, "winhttp.dll")) || Directory.Exists(Path.Combine(dir, "BepInEx")))
            return dir;
        var subs = Directory.GetDirectories(dir);
        return subs.Length == 1 ? subs[0] : dir;
    }

    private static void EnsureGameClosed(string gameDir)
    {
        var proxy = Path.Combine(gameDir, "winhttp.dll");
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
