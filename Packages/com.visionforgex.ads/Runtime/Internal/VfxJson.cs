using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VfxAds
{
    /// <summary>
    /// A small, strict JSON reader for the bridges' messages (Unity's JsonUtility cannot read a map of details).
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers long or double, true/false bool,
    /// null null. A malformed text gives null, never an exception.
    /// </summary>
    internal static class VfxJson
    {
        public static object Parse(string text)
        {
            if (text == null) return null;
            try
            {
                int i = 0;
                var v = Value(text, ref i);
                Ws(text, ref i);
                return i == text.Length ? v : null;
            }
            catch (FormatException) { return null; }
            catch (IndexOutOfRangeException) { return null; }
            catch (ArgumentOutOfRangeException) { return null; }
            catch (OverflowException) { return null; }
        }

        public static Dictionary<string, object> Object(string text) => Parse(text) as Dictionary<string, object>;

        static void Ws(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("end");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == 't') { Word(s, ref i, "true"); return true; }
            if (c == 'f') { Word(s, ref i, "false"); return false; }
            if (c == 'n') { Word(s, ref i, "null"); return null; }
            return Num(s, ref i);
        }

        static void Word(string s, ref int i, string w)
        {
            if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) throw new FormatException(w);
            i += w.Length;
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                if (s[i] != '"') throw new FormatException("key");
                var k = Str(s, ref i);
                Ws(s, ref i);
                if (s[i] != ':') throw new FormatException(":");
                i++;
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("object");
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var a = new List<object>();
            i++;
            Ws(s, ref i);
            if (s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                throw new FormatException("array");
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException("escape");
                }
            }
        }

        static object Num(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            while (i < s.Length && ("0123456789+-.eE".IndexOf(s[i]) >= 0)) i++;
            var t = s.Substring(start, i - start);
            if (t.Length == 0 || t == "-") throw new FormatException("number");
            if (t.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 && long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)) return l;
            return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ----- reading values out of a parsed object, forgiving about types

        public static string S(Dictionary<string, object> d, string k, string def = "") =>
            d != null && d.TryGetValue(k, out var v) && v != null ? (v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture)) : def;

        public static string SOrNull(Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out var v) && v != null ? (v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture)) : null;

        public static long L(Dictionary<string, object> d, string k, long def = 0)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
            if (v is long l) return l;
            if (v is double db) return (long)Math.Round(db);
            if (v is string s && long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var p)) return p;
            return def;
        }

        public static bool B(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is bool b && b;

        public static Dictionary<string, object> O(Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
    }

    /// <summary>Writing JSON values (for the options and the Editor's files).</summary>
    internal static class VfxJsonOut
    {
        public static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string Array(IEnumerable<string> items)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            if (items != null)
                foreach (var s in items)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    if (!first) sb.Append(',');
                    sb.Append(Str(s.Trim()));
                    first = false;
                }
            return sb.Append(']').ToString();
        }
    }
}
