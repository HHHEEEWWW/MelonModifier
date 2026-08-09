namespace MelonModifier.Core.Models;

/// <summary>MelonLoader 的一个发布版本（来自 GitHub Releases API）。</summary>
public sealed class MelonLoaderRelease
{
    public string Tag { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTimeOffset? PublishedAt { get; set; }

    public string? Body { get; set; }

    /// <summary>Windows x64 资产下载 URL（MelonLoader.x64.zip）。</summary>
    public string? WindowsX64Url { get; set; }

    public string VersionLabel => string.IsNullOrWhiteSpace(Tag) ? Name : Tag;
}
