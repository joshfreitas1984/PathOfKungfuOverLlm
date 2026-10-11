using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PathOfKungfuPlugin;

/// <summary>
/// The reverse of <see cref="JsonFlattener"/>: puts translated strings back into a JSON cell by the paths the flattener
/// wrote (<c>.name</c>, <c>[2]</c>, <c>{json}</c>, <c>;1</c>). The walk mirrors the flattener's, so a path means the same
/// thing in both. Strings are replaced by path whether or not the string being replaced is Chinese: the game's English
/// workbooks hold English in the same places.
/// </summary>
internal static class JsonInjector
{
    private static readonly Regex Cjk = new(@"[一-鿿]", RegexOptions.Compiled);
    private static readonly Regex CodeField = new(@"^-?[0-9:.]*$", RegexOptions.Compiled);
    private static readonly JsonSerializerSettings Settings = new() { DateParseHandling = DateParseHandling.None };

    internal sealed class Result
    {
        public string Json = "";
        public int Applied;
        public readonly HashSet<string> Used = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the cell with <paramref name="translations"/> (path to text) applied. <see cref="Result.Used"/> holds the paths
    /// that were found; the rest did not exist in this cell. A cell that is not JSON, or has nothing to change, comes back as is.
    /// </summary>
    public static Result Apply(string cell, Dictionary<string, string> translations)
    {
        var result = new Result { Json = cell };
        if (string.IsNullOrEmpty(cell) || translations.Count == 0 || !LooksLikeJson(cell)) return result;

        JToken? root;
        try { root = JsonConvert.DeserializeObject<JToken>(cell, Settings); }
        catch (JsonException) { return result; }
        if (root == null) return result;

        if (Walk(root, "", null, translations, result))
            result.Json = root.ToString(Formatting.None);
        return result;
    }

    private static bool LooksLikeJson(string s)
    {
        var t = s.Trim();
        return t.Length > 1 && ((t[0] == '{' && t[t.Length - 1] == '}') || (t[0] == '[' && t[t.Length - 1] == ']'));
    }

    private static bool Walk(JToken token, string path, string? property, Dictionary<string, string> translations, Result result)
    {
        var changed = false;
        switch (token.Type)
        {
            case JTokenType.Object:
                foreach (var p in ((JObject)token).Properties().ToList())
                    changed |= Walk(p.Value, path + "." + p.Name, p.Name, translations, result);
                break;
            case JTokenType.Array:
                var i = 0;
                foreach (var item in ((JArray)token).ToList())
                    changed |= Walk(item, path + "[" + i++ + "]", null, translations, result);
                break;
            case JTokenType.String:
                var text = (string?)token ?? "";
                if (LooksLikeJson(text))
                {
                    try
                    {
                        var inner = JsonConvert.DeserializeObject<JToken>(text, Settings);
                        if (inner != null)
                        {
                            if (!Walk(inner, path + "{json}", null, translations, result)) return false;
                            token.Replace(new JValue(inner.ToString(Formatting.None)));
                            return true;
                        }
                    }
                    catch (JsonException) { /* plain text that happens to be bracketed */ }
                }

                if (property == "triggerValue") return false;
                if (property == "action")
                {
                    var action = Action(text, path, translations, result);
                    if (action == null) return false;
                    token.Replace(new JValue(action));
                    return true;
                }

                if (translations.TryGetValue(path, out var translated))
                {
                    result.Used.Add(path);
                    result.Applied++;
                    if (translated == text) return false;
                    token.Replace(new JValue(translated));
                    changed = true;
                }
                break;
        }
        return changed;
    }

    /// <summary>
    /// An action is ";"-separated segments of "code,code,...,message". Each segment has its own path (";N"). The message is
    /// what follows the leading code fields: the first field with Chinese in the source, or in the English workbooks the
    /// first field that is not a number or a code like 16:27000.
    /// </summary>
    private static string? Action(string text, string path, Dictionary<string, string> translations, Result result)
    {
        var segments = text.Split(';');
        var changed = false;
        for (var s = 0; s < segments.Length; s++)
        {
            var key = path + ";" + s;
            if (!translations.TryGetValue(key, out var translated)) continue;

            var fields = segments[s].Split(',');
            var first = 0;
            if (Cjk.IsMatch(segments[s]))
                while (first < fields.Length && !Cjk.IsMatch(fields[first])) first++;
            else
                while (first < fields.Length && CodeField.IsMatch(fields[first])) first++;
            if (first >= fields.Length) continue;

            result.Used.Add(key);
            result.Applied++;
            // The game splits an action on ";", so a ";" in the message would cut it in two.
            var message = translated.Replace(';', ',');
            var replaced = string.Join(",", fields, 0, first) + (first > 0 ? "," : "") + message;
            if (replaced == segments[s]) continue;
            segments[s] = replaced;
            changed = true;
        }
        return changed ? string.Join(";", segments) : null;
    }
}
