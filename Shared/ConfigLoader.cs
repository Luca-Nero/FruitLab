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
    internal static class ConfigLoader
    {
        private const int BannerWidth = 62;

        public static string IniPath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            ConfigHelp.FileName);

        public static void Load()
        {
            try
            {
                if (!File.Exists(IniPath))
                {
                    Write();
                    MelonLogger.Msg($"[{ConfigHelp.Mod}] Wrote default {ConfigHelp.FileName}");
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

                Write();
                MelonLogger.Msg($"[{ConfigHelp.Mod}] {ConfigHelp.FileName} loaded.");
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[{ConfigHelp.Mod}] Config load failed: {e.Message}");
            }
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

        private static bool IsRenderable(Type t) =>
            t == typeof(bool) || t == typeof(float) || t == typeof(int) ||
            t == typeof(string) || t == typeof(KeyCode);

        private static void Write()
        {
            var sb = new StringBuilder();

            string title = $"{ConfigHelp.Mod} v{ConfigHelp.Version}  —  Configuration";
            int    room  = Math.Max(BannerWidth - title.Length, 0);

            sb.AppendLine("# ╔" + new string('═', BannerWidth) + "╗");
            sb.AppendLine("# ║" + new string(' ', room / 2) + title +
                                  new string(' ', room - room / 2) + "║");
            sb.AppendLine("# ╚" + new string('═', BannerWidth) + "╝");
            sb.AppendLine("# Reload requires a game restart. All floats use . as decimal separator.");
            sb.AppendLine();

            var order      = new List<string>();
            var byCategory = new Dictionary<string, List<FieldInfo>>();

            foreach (var f in typeof(Config).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.IsSpecialName || !IsRenderable(f.FieldType)) continue;
                var attr = (FruitLib.MenuCategoryAttribute)Attribute.GetCustomAttribute(
                    f, typeof(FruitLib.MenuCategoryAttribute));
                if (attr == null) continue;

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
                    if (ConfigHelp.Help.TryGetValue(f.Name, out var help))
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
