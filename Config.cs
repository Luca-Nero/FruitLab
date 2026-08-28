using Il2Cpp;
using Il2CppEffectors;
using Il2CppEffectors.ReceiveMethods.Index;
using Il2CppInterop.Runtime;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

namespace DoNoHarm
{
    internal static class Config
    {
        // ── Controls ──────────────────────────────────────────────────────────────
        [FruitLib.MenuCategory("Controls")] public static KeyCode SyringeKey = KeyCode.Z;
        [FruitLib.MenuCategory("Controls")] public static KeyCode RecallKey = KeyCode.X;
        [FruitLib.MenuCategory("Controls")] public static KeyCode DebugVisKey = KeyCode.F10;

        // ── Healing Syringe ───────────────────────────────────────────────────────

        [FruitLib.MenuCategory("Healing Syringe")] public static float ThrowSpeed = 16f;
        /// Seconds between heal ticks (controls overall speed).
        [FruitLib.MenuCategory("Healing Syringe")] public static float HealTickInterval = 0.05f;
        /// Seconds to idle before rechecking for wounds after queue is exhausted.
        [FruitLib.MenuCategory("Healing Syringe")] public static float RecheckInterval = 0.1f;
        /// Heal signal per voxel — large enough to counter deep wounds.
        [FruitLib.MenuCategory("Healing Syringe")] public static float HealSignal = 1000000f;
        /// How many voxel-units the heal wave expands per tick (per limb).
        /// Low = slow Wolverine crawl, high = sharp pop.
        [FruitLib.MenuCategory("Healing Syringe")] public static float HealWaveSpeed = 0.25f;
        /// World-space meters/sec the heal wave front propagates across limbs.
        /// Controls stagger between limbs starting to heal.
        [FruitLib.MenuCategory("Healing Syringe")] public static float HealWorldSpeed = 3f;

        // ── Debug ──────────────────────────────────────────────────────────────────
        [FruitLib.MenuCategory("Debug")] public static bool DebugVis = false;

    }

    internal static class ConfigLoader
    {
        public static string IniPath => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "DoNoHarm.ini");
        private static string ConfigPath => IniPath;

        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) { Write(); MelonLogger.Msg("Wrote default DoNoHarm.ini"); return; }
                foreach (var line in File.ReadAllLines(ConfigPath))
                {
                    string t = line.Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;
                    int eq = t.IndexOf('=');
                    if (eq < 0) continue;
                    SetField(t.Substring(0, eq).Trim(), t.Substring(eq + 1).Trim());
                }
                // Re-write: adds new fields, removes stale ones, preserves user values
                Write();
                MelonLogger.Msg("DoNoHarm.ini loaded.");
            }
            catch (Exception e) { MelonLogger.Warning($"Config load failed: {e.Message}"); }
        }

        private static void SetField(string key, string value)
        {
            var f = typeof(Config).GetField(key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (f == null) return;
            try
            {
                if (f.FieldType == typeof(float)) f.SetValue(null, float.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
                else if (f.FieldType == typeof(int)) f.SetValue(null, int.Parse(value));
                else if (f.FieldType == typeof(bool)) f.SetValue(null, value.ToLower() == "true");
                else if (f.FieldType == typeof(string)) f.SetValue(null, value);
                else if (f.FieldType == typeof(KeyCode)) f.SetValue(null, (KeyCode)Enum.Parse(typeof(KeyCode), value, true));
            }
            catch { }
        }

        private static void Write()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# ╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("# ║              Do no Harm v1.0.0  —  Configuration             ║");
            sb.AppendLine("# ╚══════════════════════════════════════════════════════════════╝");
            sb.AppendLine("# All floats use . as decimal separator.");
            sb.AppendLine();

            // ── Controls ─────────────────────────────────────────────────────────────
            sb.AppendLine("# Syringe Key    : key to throw a syringe");
            sb.AppendLine("# Recall Key     : key to delete all syringes");
            sb.AppendLine("# Debug Key      : key to toggle debug logging/visualization [BROKEN]");
            sb.AppendLine($"Syringe Key = {Config.SyringeKey}");
            sb.AppendLine($"Recall Key = {Config.RecallKey}");
            sb.AppendLine($"Debug Key = {Config.DebugVisKey}");
            sb.AppendLine();

            // ── Healing Syringe ──────────────────────────────────────────────────────
            sb.AppendLine("# ── Healing Syringe ─────────────────────────────────────────────");
            sb.AppendLine($"Throw Speed = initial throw velocity (m/s)");
            sb.AppendLine($"Heal Tick Interval = Seconds between heal ticks (controls overall speed).");
            sb.AppendLine($"Recheck Interval =  Seconds to idle before rechecking for wounds after queue is exhausted.");
            sb.AppendLine($"Heal Signal = Heal signal per voxel. Default is large enough to counter deep wounds.");
            sb.AppendLine($"Heal Wave Speed = How many voxel-units the heal wave expands per tick (per limb). Low = slow Wolverine crawl, high = sharp pop.");
            sb.AppendLine($"Heal World Speed = World-space meters/sec the heal wave front propagates across limbs. Controls stagger between limbs starting to heal.");
            sb.AppendLine($"Throw Speed = {F(Config.ThrowSpeed)}");
            sb.AppendLine($"Heal Tick Interval = {F(Config.HealTickInterval)}");
            sb.AppendLine($"Recheck Interval = {F(Config.RecheckInterval)}");
            sb.AppendLine($"Heal Signal = {F(Config.HealSignal)}");
            sb.AppendLine($"Heal Wave Speed = {F(Config.HealWaveSpeed)}");
            sb.AppendLine($"Heal World Speed = {F(Config.HealWorldSpeed)}");
            sb.AppendLine();

            // ── Debug ────────────────────────────────────────────────────────────────
            sb.AppendLine("# ── Debug ───────────────────────────────────────────────────────");
            sb.AppendLine("# DebugVis    : Placeholder bool");
            sb.AppendLine($"DebugDrawRays = {B(Config.DebugVis)}");

            File.WriteAllText(ConfigPath, sb.ToString());
        }
        private static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        private static string B(bool v) => v ? "true" : "false";
    }

}
