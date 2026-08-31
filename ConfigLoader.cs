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

            ["RotSpreadSeconds"] = "seconds the rot takes to cross a joint into the next limb",
            ["RotSpreadAt"]      = "how far through a limb the discolouration gets before the next limb is infected (0-1)",
            ["RotWaveSpeed"]     = "voxel-units the discolouration front advances per tick",
            ["RotNecrosisLag"]   = "voxel-units the blackening trails behind the discolouration",
            ["RotDeathLag"]      = "voxel-units the destruction trails behind the blackening — how long dead flesh is left standing before it goes",
            ["RotTickInterval"]  = "seconds between rot ticks",
            ["RotDamage"]        = "destruction dealt per rotted voxel; high enough that flesh actually goes",
            ["RotPitting"]       = "percent of the discoloured front eaten away outright — the surface only re-meshes where something was destroyed, so this is what makes the creeping rot visible at all; also just looks like rotting flesh",
            ["RotMarkDamage"]    = "damage the leading discoloured front deals — 1 just registers the rot with the body without taking anything, raise it to make the creature suffer as it spreads, 0 disables it",

            ["SutureAimRange"]   = "how far you can be from a limb and still click it",
            ["SutureCarryDistance"] = "how far in front of you a picked-up limb is held to start with; scroll moves it while carrying",
            ["SutureTurnStep"]      = "degrees a scroll notch turns a carried limb; lower for finer placement",
            ["SutureRollKey"]       = "hold and scroll to spin a carried limb about the way you are looking",
            ["SutureTiltKey"]       = "hold and scroll to tip a carried limb up and down",
            ["SutureCarryRight"]    = "how far to the side a carried limb is held, as a fraction of how far out it is, so it keeps the same spot on screen at any reach; negative holds it to the left",
            ["SutureCarryUp"]       = "how far above the crosshair a carried limb is held, same units — with SutureCarryRight this keeps the limb from covering the spot you are aiming at",
            ["SutureSeamOffset"]    = "how far off the surface the seam is built, in metres; the game's own limb joints sit set back from where two limbs meet rather than on the skin, and clicking a surface gives a point that is too deep. Negative sinks the limb into the body instead",
            ["SutureSeamCollision"]  = "let the sutured limb collide with the body it was sewn to. Leave this off. Two limbs meeting at a joint overlap by design \u2014 that is what a seam is \u2014 so the limb at the join grinds against its new neighbour forever, and the game reads that as impact damage: a sphere of destroyed flesh at the seam, which disconnects the limb and severs it again. Only that one limb is excused; anything hanging off it still collides normally",
            ["SutureBreakForce"] = "force needed to tear a sutured limb off again; 0 makes the seam unbreakable",
            ["SutureSettle"]     = "seconds of collision-damage immunity a freshly sutured limb and whatever it touches get; without it the limb becoming solid inside a chest punches a hole in both, and the game severs it again where the flesh went. 0 disables",
            ["SutureGhost"]      = "show a hologram of the limb where it would attach while you carry it",
            ["SutureNative"]     = "when a limb is still one of the body's own, put it back in its actual socket rather than where you clicked. That slot is where its node tag lives, and the puppeteer only knows limbs by tag, so it is the one route by which a reattached limb can end up animated instead of hanging. Off means every limb goes exactly where you put it",
            ["SutureAdopt"]      = "hand the sutured limb over to the body it was sewn to, so it shares that body's vitals and gets animated instead of hanging. Without it a reattached limb sits in the right slot while still belonging to a creature of its own. The whole assembly is handed over, not just the limb at the seam, or a reattached arm ends up with a forearm and hand that still belong elsewhere",
            ["SutureGraft"]      = "also join the two in the creature hierarchy, not just physically — off gives you a limb that hangs correctly but that the body does not know it has",
            ["SutureAlign"]      = "rotate the limb so it points out of the surface you attached it to; off keeps whatever pose it was lying in",
            ["VitalsRange"] = "how far the Vitals Monitor reaches when you aim at a body, in metres",

            ["LogDiagnostics"]   = "log what an operation actually does, step by step, with the voxels each step destroys and in which limb; the answer to \"why did that leave a hole\" lives here",
            ["DiagWindow"]       = "seconds to keep reporting after an operation finishes, so anything the physics does a moment later still shows up",
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
