using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>
/// 兼容性参考目录：热门 Unity 游戏清单（内置静态数据，离线可用）。
/// 用途：给用户推荐可玩 mod 的游戏，并标注引擎类型与推荐 Mod 框架。
/// </summary>
public static class CompatibilityCatalog
{
    /// <summary>全部条目。</summary>
    public static IReadOnlyList<CompatibleGame> All { get; } = Build();

    /// <summary>按类型分组（保持清单顺序）。</summary>
    public static IReadOnlyList<IGrouping<string, CompatibleGame>> ByCategory
        => All.GroupBy(g => g.Category).ToList();

    private static List<CompatibleGame> Build() => new()
    {
        // ---- 生存 / 开放世界 ----
        new() { Name = "Rust", Category = "生存", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "Oxide/uMod 生态" },
        new() { Name = "Valheim", Category = "生存", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "联机 Mod 丰富" },
        new() { Name = "7 Days to Die", Category = "生存", Engine = GameEngine.Mono, Framework = "MelonLoader+BepInEx", Notes = "Mono 引擎，双框架友好" },
        new() { Name = "Grounded", Category = "生存", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "V Rising", Category = "生存", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "Subnautica", Category = "生存", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "The Long Dark", Category = "生存", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },

        // ---- 射击 / 战术 ----
        new() { Name = "Escape from Tarkov", Category = "射击", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "社区 Mod 生态大" },
        new() { Name = "Squad", Category = "射击", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "GTFO", Category = "射击", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },

        // ---- 合作 / 派对 / 恐怖 ----
        new() { Name = "Lethal Company", Category = "合作", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "Mod 生态巨大" },
        new() { Name = "Among Us", Category = "合作", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "BepInEx 经典案例" },
        new() { Name = "Phasmophobia", Category = "合作", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "Sons of The Forest", Category = "合作", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "Human Fall Flat", Category = "合作", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "SCP: Secret Laboratory", Category = "合作", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "MelonLoader 官方支持" },
        new() { Name = "Overcooked 2", Category = "合作", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },

        // ---- 肉鸽 / 卡牌 ----
        new() { Name = "Hades", Category = "肉鸽", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },
        new() { Name = "Slay the Spire", Category = "肉鸽", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "ModTheSpire 生态" },
        new() { Name = "Balatro", Category = "肉鸽", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "创意工坊支持" },
        new() { Name = "Risk of Rain 2", Category = "肉鸽", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "Vampire Survivors", Category = "肉鸽", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "Dead Cells", Category = "肉鸽", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },
        new() { Name = "Inscryption", Category = "肉鸽", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },
        new() { Name = "Monster Train", Category = "肉鸽", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },

        // ---- 模拟 / 策略 ----
        new() { Name = "RimWorld", Category = "模拟", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "Harmony 原生生态" },
        new() { Name = "Cities: Skylines", Category = "模拟", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "创意工坊生态巨大" },
        new() { Name = "Oxygen Not Included", Category = "模拟", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },
        new() { Name = "Kerbal Space Program", Category = "模拟", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },
        new() { Name = "Dyson Sphere Program", Category = "模拟", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },
        new() { Name = "Two Point Hospital", Category = "模拟", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },

        // ---- 动作 / 冒险 ----
        new() { Name = "Hollow Knight", Category = "动作", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "MelonLoader 经典案例" },
        new() { Name = "Cuphead", Category = "动作", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },
        new() { Name = "Ori and the Blind Forest", Category = "动作", Engine = GameEngine.Mono, Framework = "MelonLoader", Notes = "" },
        new() { Name = "Tunic", Category = "动作", Engine = GameEngine.Il2Cpp, Framework = "BepInEx", Notes = "" },

        // ---- 二次元 / 手游 ----
        new() { Name = "Genshin Impact", Category = "二次元", Engine = GameEngine.Il2Cpp, Framework = "—", Notes = "全球最大 Unity 项目之一" },
        new() { Name = "Honkai: Star Rail", Category = "二次元", Engine = GameEngine.Il2Cpp, Framework = "—", Notes = "" },
        new() { Name = "Zenless Zone Zero", Category = "二次元", Engine = GameEngine.Il2Cpp, Framework = "—", Notes = "" },
        new() { Name = "Arknights", Category = "二次元", Engine = GameEngine.Il2Cpp, Framework = "—", Notes = "手游" },
        new() { Name = "Marvel Snap", Category = "二次元", Engine = GameEngine.Il2Cpp, Framework = "—", Notes = "手游" },

        // ---- 本机实测 ----
        new() { Name = "IRON NEST: Heavy Turret Simulator", Category = "模拟", Engine = GameEngine.Il2Cpp, Framework = "MelonLoader", Notes = "本机实测：MelonLoader v0.7.3 + FCS Mod" },
    };
}
