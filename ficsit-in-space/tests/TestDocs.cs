// Offline test: parse a Satisfactory Docs-format file and check every sheet row resolves.
// usage: mono TestDocs.exe <path to Docs json (UTF-16)>
using System;
using System.IO;
using FicsitInSpace;

static class TestDocs
{
    static int fails;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static int Main(string[] args)
    {
        var text = File.ReadAllText(args[0]);               // BOM-detecting, like StreamReader in game
        var docs = SatDocs.Parse(text);
        Check(docs.ClassCount > 0, "classes parsed: " + docs.ClassCount);
        // same bytes decoded wrongly (8-bit) must still parse
        var raw = File.ReadAllBytes(args[0]);
        var sb = new System.Text.StringBuilder(); foreach (var b in raw) sb.Append((char)b);
        Check(SatDocs.Parse(sb.ToString()).ClassCount == docs.ClassCount, "8-bit decoded fallback parses");

        foreach (var it in Sheets.Items)
            Check(!string.IsNullOrEmpty(docs.DisplayName(it.SatClass)), "item " + it.Key + " -> " + docs.DisplayName(it.SatClass));
        foreach (var r in Sheets.Recipes)
        {
            var rec = docs.GetRecipe(r.SatRecipe);
            Check(rec != null && rec.Products.Count > 0 && rec.Seconds > 0, "recipe " + r.Key + " found");
            if (rec == null) continue;
            string outCls = null; foreach (var it in Sheets.Items) if (it.Key == r.Output) outCls = it.SatClass;
            Check(rec.Products[0].SatClass == outCls, "recipe " + r.Key + " makes " + outCls);
            var line = r.Key + ": ";
            foreach (var a in rec.Ingredients)
            {
                bool known = false; foreach (var it in Sheets.Items) if (it.SatClass == a.SatClass) known = true;
                Check(known && a.Count > 0, "  ingredient " + a.SatClass + " x" + a.Count + " is in items sheet");
                line += a.Count + " " + a.SatClass + " ";
            }
            Console.WriteLine("     " + line + "-> " + rec.Products[0].Count + " in " + rec.Seconds + "s");
        }
        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAIL");
        return fails == 0 ? 0 : 1;
    }
}
