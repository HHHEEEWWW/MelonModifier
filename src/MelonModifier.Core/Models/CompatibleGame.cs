namespace MelonModifier.Core.Models;

/// <summary>兼容性参考条目：热门 Unity 游戏与其 Mod 框架适配情况。</summary>
public sealed class CompatibleGame
{
    /// <summary>游戏名称。</summary>
    public string Name { get; init; } = "";

    /// <summary>类型分类（生存/射击/合作/肉鸽/模拟/动作/二次元等）。</summary>
    public string Category { get; init; } = "";

    /// <summary>引擎类型（Mono / Il2Cpp）。</summary>
    public GameEngine Engine { get; init; }

    /// <summary>推荐 Mod 框架（"MelonLoader" / "BepInEx" / "MelonLoader+BepInEx"）。</summary>
    public string Framework { get; init; } = "";

    /// <summary>MOD 生态备注。</summary>
    public string Notes { get; init; } = "";

    /// <summary>引擎可读标签。</summary>
    public string EngineLabel => Engine == GameEngine.Il2Cpp ? "Il2Cpp" : Engine == GameEngine.Mono ? "Mono" : "—";
}
