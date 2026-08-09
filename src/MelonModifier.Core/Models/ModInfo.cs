namespace MelonModifier.Core.Models;

/// <summary>游戏目录下 Mods/ 或 Plugins/ 中的一个 DLL 条目。</summary>
public sealed class ModInfo
{
    /// <summary>文件名（含扩展名）。</summary>
    public string Name { get; set; } = "";

    /// <summary>完整文件路径。</summary>
    public string FullPath { get; set; } = "";

    /// <summary>true = 来自 Plugins/，false = 来自 Mods/。</summary>
    public bool IsPlugin { get; set; }

    /// <summary>是否启用（MelonLoader 会跳过 .disabled 后缀的文件）。</summary>
    public bool IsEnabled { get; set; }

    public long SizeBytes { get; set; }

    public DateTime LastModified { get; set; }

    public string SizeLabel => SizeBytes switch
    {
        >= 1024L * 1024 * 1024 => $"{SizeBytes / 1024.0 / 1024 / 1024:F2} GB",
        >= 1024L * 1024 => $"{SizeBytes / 1024.0 / 1024:F2} MB",
        >= 1024L => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes} B",
    };
}
