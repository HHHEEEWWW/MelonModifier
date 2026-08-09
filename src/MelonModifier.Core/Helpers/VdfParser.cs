using System.Collections.Generic;
using System.Text;

namespace MelonModifier.Core.Helpers;

/// <summary>
/// 极简 Valve VDF 解析器。用于读取 Steam 的
/// libraryfolders.vdf 与 appmanifest_*.acf（KeyValue 嵌套格式）。
/// </summary>
public static class VdfParser
{
    /// <summary>
    /// 解析 VDF 文本为嵌套字典。返回 null 表示解析失败。
    /// </summary>
    public static Dictionary<string, object>? Parse(string text)
    {
        int pos = 0;
        try
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                SkipWs(text, ref pos);
                if (pos >= text.Length)
                    break;

                // VDF 顶层形如 "key" { ... }：先读 key，再进入花括号节点
                var key = ReadKey(text, ref pos);
                SkipWs(text, ref pos);
                if (pos >= text.Length || text[pos] != '{')
                    throw new FormatException("expected '{' after top-level key");
                result[key] = ParseNode(text, ref pos);
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static object ParseNode(string text, ref int pos)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        SkipWs(text, ref pos);

        if (pos >= text.Length || text[pos] != '{')
            throw new FormatException("expected '{'");

        pos++; // 跳过 '{'

        while (true)
        {
            SkipWs(text, ref pos);
            if (pos >= text.Length)
                throw new FormatException("unexpected EOF");

            if (text[pos] == '}')
            {
                pos++;
                return dict;
            }

            // key 可以是带引号字符串或裸标识符
            string key = ReadKey(text, ref pos);
            SkipWs(text, ref pos);

            if (pos < text.Length && text[pos] == '{')
            {
                dict[key] = ParseNode(text, ref pos);
            }
            else
            {
                dict[key] = ReadKey(text, ref pos); // 值（字符串）
            }
        }
    }

    private static string ReadKey(string text, ref int pos)
    {
        SkipWs(text, ref pos);
        if (pos >= text.Length)
            throw new FormatException("unexpected EOF in key");

        if (text[pos] == '"')
        {
            pos++;
            var sb = new StringBuilder();
            while (pos < text.Length && text[pos] != '"')
            {
                if (text[pos] == '\\' && pos + 1 < text.Length)
                {
                    pos++;
                    sb.Append(text[pos]);
                }
                else
                {
                    sb.Append(text[pos]);
                }
                pos++;
            }
            if (pos >= text.Length)
                throw new FormatException("unterminated quoted string");
            pos++; // 跳过闭合引号
            return sb.ToString();
        }

        // 裸标识符（到空白/引号/花括号为止）
        int start = pos;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos])
               && text[pos] != '"' && text[pos] != '{' && text[pos] != '}')
        {
            pos++;
        }
        return text[start..pos];
    }

    private static void SkipWs(string text, ref int pos)
    {
        while (pos < text.Length && char.IsWhiteSpace(text[pos]))
            pos++;
    }
}
