using System.Collections.Generic;
using System.Text;

namespace HexFall.UI
{
    // Tiny request-body builder: JsonUtility cannot write maps, and Newtonsoft is not a direct dependency.
    public sealed class CognitoJsonObject
    {
        private readonly List<string> _members = new List<string>();

        public CognitoJsonObject Str(string key, string value)
        {
            _members.Add(Quote(key) + ":" + Quote(value));
            return this;
        }

        // rawJson must already be valid JSON (a nested object or array).
        public CognitoJsonObject Raw(string key, string rawJson)
        {
            _members.Add(Quote(key) + ":" + rawJson);
            return this;
        }

        public string Build() => "{" + string.Join(",", _members) + "}";

        public static string Array(params string[] rawItems) => "[" + string.Join(",", rawItems) + "]";

        private static string Quote(string text)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in text ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }

            return sb.Append('"').ToString();
        }
    }
}
