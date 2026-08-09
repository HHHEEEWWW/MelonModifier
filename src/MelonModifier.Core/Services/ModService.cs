using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>Mods/Plugins 目录管理：浏览、启停、安装 DLL。</summary>
public sealed class ModService
{
    private const string DisabledSuffix = ".disabled";

    /// <summary>列出游戏目录下的 Mods 与 Plugins（均为 .dll）。</summary>
    public List<ModInfo> ListMods(GameInfo game)
    {
        var result = new List<ModInfo>();
        Collect(Path.Combine(game.Path, "Mods"), isPlugin: false, result);
        Collect(Path.Combine(game.Path, "Plugins"), isPlugin: true, result);
        return result
            .OrderBy(m => m.IsPlugin)
            .ThenBy(m => m.Name, System.StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>启停一个 Mod：通过 .disabled 后缀（MelonLoader 会跳过该后缀文件）。</summary>
    public void SetEnabled(ModInfo mod, bool enabled)
    {
        var target = mod.FullPath;
        if (enabled && !mod.IsEnabled)
        {
            var disabledPath = target + DisabledSuffix;
            if (File.Exists(disabledPath))
                File.Move(disabledPath, target);
        }
        else if (!enabled && mod.IsEnabled)
        {
            File.Move(target, target + DisabledSuffix);
        }
        mod.IsEnabled = enabled;
    }

    /// <summary>安装一个 DLL 到 Mods 或 Plugins。返回安装后的条目。</summary>
    public ModInfo InstallDll(GameInfo game, string sourceDll, bool isPlugin)
    {
        var folder = Path.Combine(game.Path, isPlugin ? "Plugins" : "Mods");
        Directory.CreateDirectory(folder);

        var fileName = Path.GetFileName(sourceDll);
        var dest = Path.Combine(folder, fileName);
        File.Copy(sourceDll, dest, true);

        return new ModInfo
        {
            Name = fileName,
            FullPath = dest,
            IsPlugin = isPlugin,
            IsEnabled = true,
            SizeBytes = new FileInfo(dest).Length,
            LastModified = File.GetLastWriteTime(dest),
        };
    }

    /// <summary>删除一个 Mod 文件。</summary>
    public void Delete(ModInfo mod)
    {
        if (File.Exists(mod.FullPath))
            File.Delete(mod.FullPath);
    }

    private static void Collect(string folder, bool isPlugin, List<ModInfo> into)
    {
        if (!Directory.Exists(folder))
            return;

        foreach (var file in Directory.GetFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            var lower = fileName.ToLowerInvariant();
            if (!lower.EndsWith(".dll") && !lower.EndsWith(DisabledSuffix))
                continue;

            var info = new FileInfo(file);
            into.Add(new ModInfo
            {
                Name = fileName,
                FullPath = file,
                IsPlugin = isPlugin,
                IsEnabled = !fileName.EndsWith(DisabledSuffix, System.StringComparison.OrdinalIgnoreCase),
                SizeBytes = info.Length,
                LastModified = info.LastWriteTime,
            });
        }
    }
}
