// FICSIT in Space launcher. Melty runs it right before Space Engineers on every Play.
//  1. Reads the player's own Satisfactory Docs file and writes the few classes the
//     mashup uses (names, descriptions, recipes) into the mod folder, where the
//     Space Engineers script can read it (SE scripts cannot read other folders).
//  2. Makes a "FICSIT in Space" world in the player's saves from one of Space
//     Engineers' own world templates, with this mod enabled (Load Game lists it).
// Builds with any C# compiler for .NET Framework 4 (built into Windows).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FicsitInSpace;

static class FicsitLauncher
{
    const string ModName = "FicsitInSpace";
    const string WorldName = "FICSIT in Space";
    const string Marker = "ficsit-in-space.template";
    static readonly string[] Fields = { "ClassName", "mDisplayName", "mDescription", "mIngredients", "mProduct", "mManufactoringDuration" };
    static StreamWriter log;

    static int Main(string[] args)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string modDir = Arg(args, "--mod-dir") ?? Path.Combine(Path.Combine(Path.Combine(appData, "SpaceEngineers"), "Mods"), ModName);
        string sat = Arg(args, "--satisfactory") ?? Environment.GetEnvironmentVariable("FIS_SATISFACTORY");
        string se = Arg(args, "--space-engineers") ?? Environment.GetEnvironmentVariable("FIS_SPACEENGINEERS");
        Directory.CreateDirectory(modDir);
        int rc = 0;
        using (log = new StreamWriter(Path.Combine(modDir, "launcher.log"), false, new UTF8Encoding(false)))
        {
            Log("FICSIT in Space launcher; satisfactory=" + sat + " spaceengineers=" + se);
            try { ExportDocs(sat, modDir); } catch (Exception e) { rc = 1; Log("ERROR docs: " + e); }
            try { MakeWorld(se, Arg(args, "--saves")); } catch (Exception e) { Log("ERROR world: " + e); }
            Log("done");
        }
        return rc;
    }

    static string Arg(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    static void Log(string s)
    {
        Console.WriteLine(s);
        if (log != null) { log.WriteLine(DateTime.Now.ToString("s") + " " + s); log.Flush(); }
    }

    // --- 1. Satisfactory --------------------------------------------------------

    public static string FindDocs(string satDir)
    {
        if (string.IsNullOrEmpty(satDir) || !Directory.Exists(satDir))
            throw new Exception("Satisfactory folder not found: " + satDir);
        string docsDir = Path.Combine(Path.Combine(satDir, "CommunityResources"), "Docs");
        string p = Path.Combine(docsDir, "en-US.json");
        if (File.Exists(p)) return p;
        if (Directory.Exists(docsDir))
        {
            var en = Directory.GetFiles(docsDir, "en-*.json");
            if (en.Length > 0) return en[0];
            p = Path.Combine(docsDir, "Docs.json");
            if (File.Exists(p)) return p;
            var any = Directory.GetFiles(docsDir, "*.json");
            if (any.Length > 0) return any[0];
        }
        throw new Exception("No Docs file in " + docsDir);
    }

    public static void ExportDocs(string satDir, string modDir)
    {
        string src = FindDocs(satDir);
        Log("reading " + src);
        string text = File.ReadAllText(src); // detects the UTF-16 BOM
        var root = MiniJson.Parse(text.TrimStart('﻿')) as List<object>;
        if (root == null) throw new Exception("Docs file is not a JSON array");

        var wanted = new HashSet<string>();
        foreach (var r in Sheets.Items) wanted.Add(r.SatClass);
        foreach (var r in Sheets.Recipes) wanted.Add(r.SatRecipe);

        var sb = new StringBuilder("[{\"NativeClass\":\"FicsitInSpace\",\"Classes\":[\n");
        int n = 0;
        foreach (var g in root)
        {
            var group = g as Dictionary<string, object>;
            object classes;
            if (group == null || !group.TryGetValue("Classes", out classes) || !(classes is List<object>)) continue;
            foreach (var c in (List<object>)classes)
            {
                var cls = c as Dictionary<string, object>;
                object name;
                if (cls == null || !cls.TryGetValue("ClassName", out name) || !wanted.Contains(name as string ?? "")) continue;
                if (n++ > 0) sb.Append(",\n");
                sb.Append('{');
                bool first = true;
                foreach (var f in Fields)
                {
                    object v;
                    if (!cls.TryGetValue(f, out v) || !(v is string)) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    Str(sb, f); sb.Append(':'); Str(sb, (string)v);
                }
                sb.Append('}');
            }
        }
        sb.Append("\n]}]\n");
        if (n < wanted.Count) Log("WARNING: found " + n + " of " + wanted.Count + " classes the mashup uses");

        string outDir = Path.Combine(modDir, "Satisfactory");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "Docs.json"), sb.ToString(), new UTF8Encoding(false));
        Log("wrote " + n + " Satisfactory classes for the mod");
    }

    static void Str(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char ch in s)
        {
            if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
            else if (ch == '\n') sb.Append("\\n");
            else if (ch == '\r') sb.Append("\\r");
            else if (ch == '\t') sb.Append("\\t");
            else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
            else sb.Append(ch);
        }
        sb.Append('"');
    }

    // --- 2. Space Engineers world template -------------------------------------

    // The world goes into the player's own saves (%AppData%/SpaceEngineers/Saves/<id>/),
    // made from one of the game's own world templates, so Load Game lists it.
    public static void MakeWorld(string seDir, string savesRoot)
    {
        if (string.IsNullOrEmpty(seDir)) { Log("no Space Engineers folder given; skipping world"); return; }
        string worlds = Path.Combine(Path.Combine(seDir, "Content"), "CustomWorlds");
        if (!Directory.Exists(worlds)) { Log("no CustomWorlds folder at " + worlds); return; }
        if (savesRoot == null)
            savesRoot = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineers"), "Saves");
        string source = PickTemplate(worlds);
        if (source == null) { Log("no Space Engineers world template found to start from"); return; }

        // One folder per Steam account. A player who never saved has none yet: use the
        // account Steam is signed in with.
        var users = new List<string>();
        if (Directory.Exists(savesRoot))
            foreach (var d in Directory.GetDirectories(savesRoot))
            {
                ulong n;
                if (ulong.TryParse(Path.GetFileName(d), out n)) users.Add(d);
            }
        if (users.Count == 0)
        {
            string steamId = CurrentSteamId(seDir);
            if (steamId == null) { Log("no Space Engineers account folder in " + savesRoot + " and no Steam account found"); return; }
            string d = Path.Combine(savesRoot, steamId);
            Directory.CreateDirectory(d);
            users.Add(d);
            Log("created saves folder for Steam account " + steamId);
        }

        foreach (var user in users)
        {
            ulong id = ulong.Parse(Path.GetFileName(user));
            string target = Path.Combine(user, WorldName);
            if (Directory.Exists(target))
            {
                Log(File.Exists(Path.Combine(target, Marker)) ? "world already there for " + id : "a world named '" + WorldName + "' exists and is not ours; leaving it");
                continue;
            }
            Log("making world for " + id + " from template " + Path.GetFileName(source));
            CopyDir(source, target);
            File.WriteAllText(Path.Combine(target, Marker), "Made by the FICSIT in Space launcher from the '" + Path.GetFileName(source) + "' template.\n");
            bool modAdded = false;
            foreach (var f in new[] { "Sandbox.sbc", "Sandbox_config.sbc" })
            {
                string p = Path.Combine(target, f);
                if (!File.Exists(p)) continue;
                string x = File.ReadAllText(p);
                string y = PatchSandbox(x, ref modAdded);
                if (y != x) File.WriteAllText(p, y, new UTF8Encoding(false));
            }
            Log(modAdded ? "world ready with the mod enabled" : "WARNING: could not enable the mod in the world");
        }
    }

    // SteamID64 of the most recent Steam login, from Steam's config/loginusers.vdf.
    static string CurrentSteamId(string seDir)
    {
        var candidates = new List<string>();
        try
        {
            object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            if (v is string) candidates.Add((string)v);
        }
        catch (Exception e) { Log("registry: " + e.Message); }
        // the game usually sits in <steam>/steamapps/common/SpaceEngineers
        var up = Directory.GetParent(Path.GetFullPath(seDir));
        for (int i = 0; i < 2 && up != null; i++) up = up.Parent; // common -> steamapps -> steam
        if (up != null) candidates.Add(up.FullName);

        foreach (var steam in candidates)
        {
            string vdf = Path.Combine(Path.Combine(steam, "config"), "loginusers.vdf");
            if (!File.Exists(vdf)) continue;
            string found = ParseLoginUsers(File.ReadAllText(vdf));
            if (found != null) return found;
        }
        return null;
    }

    // "users" { "7656119...": { ... "MostRecent" "1" ... } }
    public static string ParseLoginUsers(string vdf)
    {
        string first = null;
        int i = 0;
        while (true)
        {
            int q = vdf.IndexOf('"', i);
            if (q < 0) break;
            int e = vdf.IndexOf('"', q + 1);
            if (e < 0) break;
            string tok = vdf.Substring(q + 1, e - q - 1);
            i = e + 1;
            ulong n;
            if (tok.Length == 17 && ulong.TryParse(tok, out n))
            {
                if (first == null) first = tok;
                int open = vdf.IndexOf('{', i), close = vdf.IndexOf('}', i);
                if (open >= 0 && close > open)
                {
                    string block = vdf.Substring(open, close - open);
                    int m = block.IndexOf("\"MostRecent\"", StringComparison.OrdinalIgnoreCase);
                    if (m >= 0 && block.IndexOf("\"1\"", m + 12, StringComparison.Ordinal) >= 0
                        && block.IndexOf("\"1\"", m + 12, StringComparison.Ordinal) - (m + 12) < 8)
                        return tok;
                }
            }
        }
        return first;
    }

    static string PickTemplate(string worlds)
    {
        var dirs = Directory.GetDirectories(worlds);
        foreach (var pref in new[] { "asteroid", "empty", "star system" })
            foreach (var d in dirs)
                if (Path.GetFileName(d).ToLowerInvariant().Contains(pref) && File.Exists(Path.Combine(d, "Sandbox.sbc")))
                    return d;
        foreach (var d in dirs)
            if (File.Exists(Path.Combine(d, "Sandbox.sbc")) && !File.Exists(Path.Combine(d, Marker)))
                return d;
        return null;
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from))
            CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }

    const string ModItem = "<ModItem FriendlyName=\"" + WorldName + "\"><Name>" + ModName + "</Name><PublishedFileId>0</PublishedFileId></ModItem>";

    public static string PatchSandbox(string x, ref bool modAdded)
    {
        x = ReplaceElement(x, "SessionName", WorldName);
        if (x.Contains("<Name>" + ModName + "</Name>")) { modAdded = true; return x; }
        if (x.Contains("<Mods />")) { modAdded = true; return x.Replace("<Mods />", "<Mods>" + ModItem + "</Mods>"); }
        int i = x.IndexOf("<Mods>", StringComparison.Ordinal);
        if (i >= 0) { modAdded = true; return x.Insert(i + "<Mods>".Length, ModItem); }
        return x;
    }

    static string ReplaceElement(string x, string tag, string value)
    {
        int a = x.IndexOf("<" + tag + ">", StringComparison.Ordinal);
        if (a < 0) return x;
        int b = x.IndexOf("</" + tag + ">", a, StringComparison.Ordinal);
        if (b < 0) return x;
        a += tag.Length + 2;
        return x.Substring(0, a) + value + x.Substring(b);
    }
}
