using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PathOfKungfuPlugin;

/// <summary>
/// Pulls the Chinese strings out of JSON stored in a spreadsheet cell. The game double-encodes:
/// a cell holds a JSON array whose <c>talks</c> property is itself a JSON string, so strings that
/// look like JSON are decoded recursively. Each Chinese leaf comes back with a path the injector can
/// follow to put the translation back:
/// <list type="bullet">
/// <item><c>.name</c> object property, <c>[2]</c> array index;</item>
/// <item><c>{json}</c> the value just before it was a JSON string that was decoded;</item>
/// <item><c>;1</c> segment 1 of a ";"-separated <c>action</c> string; the text is that segment's
/// message, after its leading code fields (e.g. <c>16:27000,25,37,1,</c>).</item>
/// </list>
/// Properties named <c>triggerValue</c> are code and are never returned.
/// </summary>
internal static class JsonFlattener
{
    private static readonly Regex Cjk = new(@"[一-鿿]", RegexOptions.Compiled);
    private static readonly JsonSerializerSettings Settings = new() { DateParseHandling = DateParseHandling.None };

    /// <summary>
    /// Writes one row per Chinese string found inside JSON cells: Row (source row), RowId (the row's
    /// first cell, the stable key), Column (1-based, matches the dumped CSV), Path (see
    /// <see cref="JsonFlattener"/>), Text. The JSON cell itself stays in the main CSV and should be
    /// skipped there when configuring translation.
    /// </summary>
    public static List<(int Row, string?[] Cells)> FlattenRows(List<string?[]> rows)
    {
        var leaves = new List<(int Row, string?[] Cells)>();
        for (var r = 1; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                foreach (var (path, text) in Flatten(rows[r][c]))
                    leaves.Add((r + 1, new string?[] { rows[r].Length > 0 ? rows[r][0] : "", (c + 1).ToString(), path, text }));
            }
        }
        return leaves;
    }

    public static List<(string Path, string Text)> Flatten(string? cell)
    {
        var result = new List<(string, string)>();
        if (!LooksLikeJson(cell)) return result;

        JToken? root;
        try { root = JsonConvert.DeserializeObject<JToken>(cell!, Settings); }
        catch (JsonException) { return result; }

        if (root != null) Walk(root, "", null, result);
        return result;
    }

    private static bool LooksLikeJson(string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var t = s!.Trim();
        return t.Length > 1 && ((t[0] == '{' && t[t.Length - 1] == '}') || (t[0] == '[' && t[t.Length - 1] == ']'));
    }

    private static void Walk(JToken token, string path, string? property, List<(string, string)> result)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                foreach (var p in ((JObject)token).Properties())
                    Walk(p.Value, path + "." + p.Name, p.Name, result);
                break;
            case JTokenType.Array:
                var i = 0;
                foreach (var item in (JArray)token)
                    Walk(item, path + "[" + i++ + "]", null, result);
                break;
            case JTokenType.String:
                var text = (string?)token ?? "";
                if (LooksLikeJson(text))
                {
                    try
                    {
                        var inner = JsonConvert.DeserializeObject<JToken>(text, Settings);
                        if (inner != null) { Walk(inner, path + "{json}", null, result); return; }
                    }
                    catch (JsonException) { /* plain text that happens to be bracketed */ }
                }
                if (property == "triggerValue" || !Cjk.IsMatch(text)) return;
                if (property == "action") Action(text, path, result);
                else result.Add((path, text));
                break;
        }
    }

    private static void Action(string text, string path, List<(string, string)> result)
    {
        var segments = text.Split(';');
        for (var s = 0; s < segments.Length; s++)
        {
            if (!Cjk.IsMatch(segments[s])) continue;
            var fields = segments[s].Split(',');
            var first = 0;
            while (first < fields.Length && !Cjk.IsMatch(fields[first])) first++;
            result.Add((path + ";" + s, string.Join(",", fields, first, fields.Length - first)));
        }
    }
}
