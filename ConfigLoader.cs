using MelonLoader;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Config ini reader / writer
    //
    // Keys are field names, matching what FruitLib's in-game menu writes. The old
    // hand-written version emitted spaced labels ("Syringe Key = Z"), which no
    // field name matched — so nothing in the file was ever read back, and every
    // change made through the menu was overwritten on the next launch.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class ConfigLoader
    {
        public static string IniPath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "FruitLab.ini");

        public static void Load()
        {
            try
            {
                if (!File.Exists(IniPath))
                {
                    Write();
                    MelonLogger.Msg("[FruitLab] Wrote default FruitLab.ini");
                    return;
                }

                foreach (var line in File.ReadAllLines(IniPath))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    int eq = t.IndexOf('=');
                    if (eq < 0) continue;
                    SetField(t.Substring(0, eq).Trim(), t.Substring(eq + 1).Trim());
                }

                // Re-write: adds new fields, drops stale ones, keeps user values.
                Write();
                MelonLogger.Msg("[FruitLab] FruitLab.ini loaded.");
            }
            catch (Exception e) { MelonLogger.Warning($"[FruitLab] Config load failed: {e.Message}"); }
        }

        private static void SetField(string key, string value)
        {
            var f = typeof(Config).GetField(key, BindingFlags.Public | BindingFlags.Static);
            if (f == null) return;
            try
            {
                if      (f.FieldType == typeof(float))   f.SetValue(null, float.Parse(value, CultureInfo.InvariantCulture));
                else if (f.FieldType == typeof(int))     f.SetValue(null, int.Parse(value, CultureInfo.InvariantCulture));
                else if (f.FieldType == typeof(bool))    f.SetValue(null, value.Equals("true", StringComparison.OrdinalIgnoreCase));
                else if (f.FieldType == typeof(string))  f.SetValue(null, value);
                else if (f.FieldType == typeof(KeyCode)) f.SetValue(null, (KeyCode)Enum.Parse(typeof(KeyCode), value, true));
            }
            catch { }
        }

        private static readonly Dictionary<string, string> FieldHelp = new Dictionary<string, string>
        {
            ["RecallKey"] = "key to destroy every syringe in the world",

            ["ThrowSpeed"]       = "initial throw velocity (m/s)",
            ["StickRadius"]      = "how close to a limb the syringe has to pass to stick, in metres — raise it if throws slip past, lower it for a needle that has to be aimed",
            ["SpentLifetime"]    = "seconds a spent syringe lies on the ground before despawning (0 = never)",
            ["HealTickInterval"] = "seconds between heal ticks — the overall healing rate",
            ["HealSignal"]       = "heal strength written into each voxel; the default is well past what any wound can undo",
            ["HealWaveSpeed"]    = "voxel-units the wave front expands per tick within one limb — low is a slow crawl, high is a sharp pop",
            ["HealWorldSpeed"]   = "metres/sec the wave front travels between limbs, which sets the stagger",

            ["LazarusDuration"] = "seconds a Lazarus dose keeps a body alive before it runs out",
            ["LazarusInterval"] = "seconds between vitals top-ups while a dose is running — lower reacts faster, higher costs less",

            ["LogVitals"] = "log each creature's LVA vitals on impact and on expiry; turn on if a body will not stay alive and you need to see which value the game is dragging back down",
        };

        private static bool IsRenderable(Type t) =>
            t == typeof(bool) || t == typeof(float) || t == typeof(int) ||
            t == typeof(string) || t == typeof(KeyCode);

        private static void Write()
        {
            var sb = new StringBuilder();

            sb.AppendLine("# ╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine($"# ║             FruitLab v{Core.Version}  —  Configuration                ║");
            sb.AppendLine("# ╚══════════════════════════════════════════════════════════════╝");
            sb.AppendLine("# Reload requires a game restart. All floats use . as decimal separator.");
            sb.AppendLine();

            var order      = new List<string>();
            var byCategory = new Dictionary<string, List<FieldInfo>>();

            foreach (var f in typeof(Config).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.IsSpecialName || !IsRenderable(f.FieldType)) continue;
                var attr = (FruitLib.MenuCategoryAttribute)Attribute.GetCustomAttribute(
                    f, typeof(FruitLib.MenuCategoryAttribute));
                if (attr == null) continue; // not a user-facing setting

                if (!byCategory.TryGetValue(attr.Name, out var list))
                {
                    list = new List<FieldInfo>();
                    byCategory[attr.Name] = list;
                    order.Add(attr.Name);
                }
                list.Add(f);
            }

            foreach (var cat in order)
            {
                sb.AppendLine($"# ── {cat} ──");
                foreach (var f in byCategory[cat])
                {
                    if (FieldHelp.TryGetValue(f.Name, out var help))
                        sb.AppendLine($"# {f.Name} : {help}");
                    sb.AppendLine($"{f.Name} = {FormatValue(f)}");
                }
                sb.AppendLine();
            }

            File.WriteAllText(IniPath, sb.ToString());
        }

        private static string FormatValue(FieldInfo f)
        {
            object val = f.GetValue(null);
            if (f.FieldType == typeof(float))
                return ((float)val).ToString("0.##############", CultureInfo.InvariantCulture);
            return val?.ToString() ?? "";
        }
    }
}
