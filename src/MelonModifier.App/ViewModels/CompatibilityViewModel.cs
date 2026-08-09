using MelonModifier.Core.Services;

namespace MelonModifier.App.ViewModels;

/// <summary>兼容性参考页：热门 Unity 游戏与 Mod 框架适配清单。</summary>
public sealed class CompatibilityViewModel
{
    /// <summary>按类型分组（类别 + 条目列表）。</summary>
    public IReadOnlyList<CategoryGroup> Categories { get; } = CompatibilityCatalog.ByCategory
        .Select(g => new CategoryGroup(g.Key, g.ToList()))
        .ToList();

    /// <summary>条目总数。</summary>
    public int TotalCount => CompatibilityCatalog.All.Count;
}

/// <summary>一个分类组（类别名 + 游戏条目）。</summary>
public sealed class CategoryGroup
{
    public string Category { get; }
    public IReadOnlyList<Core.Models.CompatibleGame> Items { get; }

    public CategoryGroup(string category, IReadOnlyList<Core.Models.CompatibleGame> items)
    {
        Category = category;
        Items = items;
    }
}
