using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonModifier.Core.Models;

namespace MelonModifier.Core.Services;

/// <summary>读取游戏目录 MelonLoader/Logs 下的日志文件。</summary>
public sealed class LogService
{
    /// <summary>返回日志文件列表（按修改时间倒序）。</summary>
    public List<FileInfo> ListLogs(GameInfo game)
    {
        var logDir = Path.Combine(game.Path, "MelonLoader", "Logs");
        if (!Directory.Exists(logDir))
            return new List<FileInfo>();

        return Directory.GetFiles(logDir)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();
    }

    /// <summary>读取最新（或指定）日志的文本，最多 maxLines 行。</summary>
    public string ReadLog(FileInfo log, int maxLines = 2000)
    {
        try
        {
            var lines = File.ReadAllLines(log.FullName);
            if (lines.Length <= maxLines)
                return string.Join('\n', lines);
            return string.Join('\n', lines.Skip(lines.Length - maxLines));
        }
        catch (IOException)
        {
            return "（日志被占用或不可读）";
        }
    }
}
