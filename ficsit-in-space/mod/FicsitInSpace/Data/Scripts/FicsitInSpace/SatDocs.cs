using System.Collections.Generic;
using System.Text;

namespace FicsitInSpace
{
    // Reads the player's own Satisfactory Docs file (CommunityResources/Docs/en-US.json).
    // Pure C#, no game API, so it can be unit-tested outside Space Engineers.
    public class SatDocs
    {
        public struct Amount
        {
            public string SatClass;
            public int Count;
            public Amount(string c, int n) { SatClass = c; Count = n; }
        }

        public class Recipe
        {
            public string ClassName, DisplayName;
            public float Seconds;
            public List<Amount> Ingredients = new List<Amount>();
            public List<Amount> Products = new List<Amount>();
        }

        // ClassName -> the class's fields (every NativeClass group merged; ClassNames are unique)
        readonly Dictionary<string, Dictionary<string, object>> classes = new Dictionary<string, Dictionary<string, object>>();

        public int ClassCount { get { return classes.Count; } }

        public static SatDocs Parse(string text)
        {
            var docs = new SatDocs();
            var root = MiniJson.Parse(Clean(text)) as List<object>;
            if (root == null)
                throw new MiniJson.JsonException("Docs root is not an array");
            foreach (var groupObj in root)
            {
                var group = groupObj as Dictionary<string, object>;
                if (group == null) continue;
                object list;
                if (!group.TryGetValue("Classes", out list)) continue;
                var arr = list as List<object>;
                if (arr == null) continue;
                foreach (var c in arr)
                {
                    var cls = c as Dictionary<string, object>;
                    if (cls == null) continue;
                    object name;
                    if (cls.TryGetValue("ClassName", out name) && name is string)
                        docs.classes[(string)name] = cls;
                }
            }
            return docs;
        }

        // The file is UTF-16. If a reader decoded it as 8-bit, every other char is NUL.
        static string Clean(string text)
        {
            if (text.IndexOf('\0') < 0)
                return text.TrimStart('﻿');
            var sb = new StringBuilder(text.Length / 2);
            foreach (char ch in text)
                if (ch != '\0' && ch != '﻿' && ch != 'ÿ' && ch != 'þ')
                    sb.Append(ch);
            return sb.ToString();
        }

        public string Field(string className, string field)
        {
            Dictionary<string, object> cls;
            object v;
            if (classes.TryGetValue(className, out cls) && cls.TryGetValue(field, out v))
                return v as string;
            return null;
        }

        public string DisplayName(string className) { return Field(className, "mDisplayName"); }
        public string Description(string className) { return Field(className, "mDescription"); }

        public Recipe GetRecipe(string className)
        {
            if (!classes.ContainsKey(className)) return null;
            var r = new Recipe { ClassName = className, DisplayName = DisplayName(className) };
            r.Seconds = ParseFloat(Field(className, "mManufactoringDuration"));
            ParseAmounts(Field(className, "mIngredients"), r.Ingredients);
            ParseAmounts(Field(className, "mProduct"), r.Products);
            return r;
        }

        // ((ItemClass="/Script/Engine.BlueprintGeneratedClass'/Game/.../Desc_IronIngot.Desc_IronIngot_C'",Amount=3),(...))
        public static void ParseAmounts(string s, List<Amount> into)
        {
            if (string.IsNullOrEmpty(s)) return;
            int i = 0;
            while ((i = s.IndexOf("ItemClass=", i)) >= 0)
            {
                i += "ItemClass=".Length;
                int end = s.IndexOf(",Amount=", i);
                if (end < 0) break;
                string path = s.Substring(i, end - i).Trim('"', '\'', ' ');
                int dot = path.LastIndexOf('.');
                string cls = (dot >= 0 ? path.Substring(dot + 1) : path).Trim('"', '\'');
                int j = end + ",Amount=".Length, n = 0;
                while (j < s.Length && s[j] >= '0' && s[j] <= '9')
                    n = n * 10 + (s[j++] - '0');
                into.Add(new Amount(cls, n));
                i = j;
            }
        }

        public static float ParseFloat(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            double whole = 0, frac = 0, scale = 1;
            bool afterDot = false;
            foreach (char ch in s)
            {
                if (ch == '.') { afterDot = true; continue; }
                if (ch < '0' || ch > '9') break;
                if (afterDot) { scale /= 10; frac += (ch - '0') * scale; }
                else whole = whole * 10 + (ch - '0');
            }
            return (float)(whole + frac);
        }
    }
}
