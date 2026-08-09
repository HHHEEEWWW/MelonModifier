using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MelonModifier.Core.Helpers;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>
/// 游戏列表持久化：手动添加的游戏 + Steam 扫描结果的缓存。
/// 缓存用于启动时立即显示上次的游戏列表，随后由后台扫描刷新。
/// </summary>
public sealed class GameRegistry
{
    private readonly string _filePath;
    private List<GameInfo> _games = new();

    public GameRegistry(string? filePath = null)
    {
        _filePath = filePath ?? AppPaths.GamesJson;
        Load();
    }

    public IReadOnlyList<GameInfo> Games => _games;

    public void Add(GameInfo game)
    {
        if (_games.Any(g => string.Equals(g.Path, game.Path, System.StringComparison.OrdinalIgnoreCase)))
            return;
        _games.Add(game);
        Save();
    }

    public void Remove(string id)
    {
        _games.RemoveAll(g => g.Id == id);
        Save();
    }

    /// <summary>以扫描/合并结果整体覆盖缓存（保留手动添加标记）。</summary>
    public void SaveAll(IEnumerable<GameInfo> games)
    {
        _games = games.ToList();
        Save();
    }

    private void Load()
    {
        AppPaths.EnsureCreated();
        if (!File.Exists(_filePath))
            return;
        try
        {
            var json = File.ReadAllText(_filePath);
            _games = JsonSerializer.Deserialize<List<GameInfo>>(json) ?? new List<GameInfo>();
        }
        catch
        {
            _games = new List<GameInfo>();
        }
    }

    private void Save()
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(_games, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
