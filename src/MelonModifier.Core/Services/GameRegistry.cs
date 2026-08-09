using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MelonModifier.Core.Helpers;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>
/// 用户手动添加的游戏列表持久化（Steam 扫描结果是动态的，不持久化）。
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
