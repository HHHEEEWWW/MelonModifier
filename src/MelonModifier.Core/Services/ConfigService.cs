using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>
/// 读写游戏的 UserData/Loader.cfg（TOML 格式的 MelonLoader 配置）。
/// 结构化展示用轻量解析；编辑采用全文文本模式（保留注释与未知键）。
/// </summary>
public sealed class ConfigService
{
    /// <summary>Loader.cfg 的完整路径，不存在返回 null。</summary>
    public string? GetConfigPath(GameInfo game)
    {
        var path = Path.Combine(game.Path, "UserData", "Loader.cfg");
        return File.Exists(path) ? path : null;
    }

    /// <summary>读取配置原文与结构化键值（用于展示）。</summary>
    public (string Raw, List<ConfigEntry> Entries)? Read(GameInfo game)
    {
        var path = GetConfigPath(game);
        if (path is null)
            return null;

        var raw = File.ReadAllText(path);
        return (raw, ParseEntries(raw));
    }

    /// <summary>以全文方式覆盖写入 Loader.cfg（保留注释与未知键）。</summary>
    public void Write(GameInfo game, string content)
    {
        var path = Path.Combine(game.Path, "UserData", "Loader.cfg");
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    /// <summary>轻量解析：[section] 下的 key = value 条目。</summary>
    public static List<ConfigEntry> ParseEntries(string raw)
    {
        var entries = new List<ConfigEntry>();
        string section = "";
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                continue;

            var sectionMatch = Regex.Match(trimmed, @"^\[([^\]]+)\]$");
            if (sectionMatch.Success)
            {
                section = sectionMatch.Groups[1].Value.Trim();
                continue;
            }

            var kv = Regex.Match(trimmed, @"^([A-Za-z0-9_\.\-]+)\s*=\s*(.*)$");
            if (kv.Success)
            {
                var value = kv.Groups[2].Value.Trim();
                // 去掉字符串引号
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                    value = value[1..^1];
                entries.Add(new ConfigEntry(section, kv.Groups[1].Value.Trim(), value));
            }
        }
        return entries;
    }
}

/// <summary>一条解析出的配置项（section.key = value）。</summary>
public sealed class ConfigEntry
{
    public ConfigEntry(string section, string key, string value)
    {
        Section = section;
        Key = key;
        Value = value;
    }

    public string Section { get; }
    public string Key { get; }
    public string Value { get; }

    public string DisplayKey => string.IsNullOrEmpty(Section) ? Key : $"{Section}.{Key}";
}
