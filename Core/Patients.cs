using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FruitLab
{
    internal static class Patients
    {
        public static string ListPath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "FruitLab.names.txt");

        private static readonly List<string> _given   = new List<string>();
        private static readonly List<string> _family  = new List<string>();

        private static readonly List<string> _special = new List<string>();

        private static readonly Dictionary<int, string> _named = new Dictionary<int, string>();
        private static readonly HashSet<string>         _taken = new HashSet<string>();

        private static readonly HashSet<int>            _rare  = new HashSet<int>();

        private static bool _loaded;

        // ══════════════════════════════════════════════════════════════════════════
        // Naming
        // ══════════════════════════════════════════════════════════════════════════

        public static string NameFor(Transform root)
        {
            string plain = Plain(root);
            if (root == null || !Config.VitalsNames || !Human(plain)) return plain;

            int id;
            try { id = root.gameObject.GetInstanceID(); } catch { return plain; }

            if (_named.TryGetValue(id, out string had)) return had;

            if (!_loaded) Load();
            if (_given.Count == 0 || _family.Count == 0) return plain;

            string name = Rare(id);
            if (name != null) _rare.Add(id);
            else              name = Compose(id);

            _named[id] = name;
            _taken.Add(name);
            return name;
        }

        public static bool IsRare(Transform root)
        {
            if (root == null || _rare.Count == 0) return false;

            try { return _rare.Contains(root.gameObject.GetInstanceID()); }
            catch { return false; }
        }

        private static string Rare(int id)
        {
            int odds = Config.VitalsSpecialOdds;
            if (odds <= 0 || _special.Count == 0) return null;

            if (Mix(id, 0xc2b2ae35u) % (uint)odds != 0u) return null;

            int start = (int)(Mix(id, 0x27d4eb2fu) % (uint)_special.Count);

            for (int step = 0; step < _special.Count; step++)
            {
                string candidate = _special[(start + step) % _special.Count];
                if (!_taken.Contains(candidate)) return candidate;
            }

            return null;
        }

        public static string RecordFor(Transform root)
        {
            if (root == null) return "----";

            try { return (Mix(root.gameObject.GetInstanceID(), 0x9e3779b9u) % 9000u + 1000u).ToString(); }
            catch { return "----"; }
        }

        public static int RestingBpm(Transform root)
        {
            int lo = Mathf.Clamp(Mathf.Min(Config.VitalsBpmRestMin, Config.VitalsBpmRestMax), 35, 200);
            int hi = Mathf.Clamp(Mathf.Max(Config.VitalsBpmRestMin, Config.VitalsBpmRestMax), lo, 200);

            if (root == null || hi == lo) return hi;

            try
            {
                int span = hi - lo + 1;
                return lo + (int)(Mix(root.gameObject.GetInstanceID(), 0x165667b1u) % (uint)span);
            }
            catch { return (lo + hi) / 2; }
        }

        public static int PressureShift(Transform root)
        {
            if (root == null) return 0;

            try { return (int)(Mix(root.gameObject.GetInstanceID(), 0xd3a2646cu) % 17u) - 8; }
            catch { return 0; }
        }

        private static string Plain(Transform root)
        {
            if (root == null) return "unknown";
            try { return root.name.Replace("(Clone)", "").Replace("Prefab", ""); }
            catch { return "unknown"; }
        }

        private static bool Human(string plain) =>
            plain != null && plain.IndexOf("human", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Compose(int id)
        {
            int g = (int)(Mix(id, 0x2545f491u) % (uint)_given.Count);
            int f = (int)(Mix(id, 0x85ebca6bu) % (uint)_family.Count);

            int limit = _given.Count * _family.Count;
            for (int step = 0; step < limit; step++)
            {
                string candidate = _given[g] + " " + _family[f];
                if (!_taken.Contains(candidate)) return candidate;

                f++;
                if (f < _family.Count) continue;

                f = 0;
                g = (g + 1) % _given.Count;
            }

            return _given[g] + " " + _family[f] + " " + (_named.Count + 1);
        }

        private static uint Mix(int value, uint salt)
        {
            uint h = (uint)value ^ salt;
            h ^= h >> 16; h *= 0x7feb352du;
            h ^= h >> 15; h *= 0x846ca68bu;
            h ^= h >> 16;
            return h;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // The list
        // ══════════════════════════════════════════════════════════════════════════

        public static void Reload()
        {
            _loaded = false;
            _named.Clear();
            _taken.Clear();
            _rare.Clear();
        }

        private static void Load()
        {
            _loaded = true;
            _given.Clear();
            _family.Clear();
            _special.Clear();

            try
            {
                if (!File.Exists(ListPath))
                {
                    File.WriteAllText(ListPath, Template());
                    MelonLogger.Msg("[FruitLab] Wrote default FruitLab.names.txt");
                }

                Parse(File.ReadAllLines(ListPath));
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Could not read FruitLab.names.txt: {e.Message}");
            }

            if (_given.Count == 0 || _family.Count == 0)
            {
                Parse(Template().Split('\n'));
                if (_given.Count == 0 || _family.Count == 0) return;
            }

            MelonLogger.Msg($"[FruitLab] {_given.Count} given and {_family.Count} family " +
                            $"names loaded ({_given.Count * _family.Count} patients), " +
                            $"and {_special.Count} special.");
        }

        private static void Parse(string[] lines)
        {
            var into = _given;

            foreach (var raw in lines)
            {
                string line = raw == null ? null : raw.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                if (line[0] == '[')
                {
                    if (line.IndexOf("family",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                        line.IndexOf("surname", StringComparison.OrdinalIgnoreCase) >= 0)
                        into = _family;
                    else if (line.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0)
                        into = _special;
                    else
                        into = _given;
                    continue;
                }

                if (!into.Contains(line)) into.Add(line);
            }
        }

        private static string Template()
        {
            var sb = new StringBuilder();

            sb.AppendLine("# FruitLab patient names.");
            sb.AppendLine("#");
            sb.AppendLine("# The Vitals Monitor names each body from these two lists, so the patient you");
            sb.AppendLine("# are treating is one you can tell apart from the body lying next to them.");
            sb.AppendLine("# A body keeps its name for as long as it exists.");
            sb.AppendLine("#");
            sb.AppendLine("# One name per line. Add as many as you like - every given name you add pairs");
            sb.AppendLine("# with every family name below, so the list grows faster than you type it.");
            sb.AppendLine("# Lines starting with # are ignored. Delete this file to get the defaults back,");
            sb.AppendLine("# or turn VitalsNames off in FruitLab.ini to go back to raw object names.");
            sb.AppendLine();

            sb.AppendLine("[given]");
            foreach (var n in Given) sb.AppendLine(n);
            sb.AppendLine();

            sb.AppendLine("[family]");
            foreach (var n in Family) sb.AppendLine(n);
            sb.AppendLine();

            sb.AppendLine("# Whole names, used as written, and only about one body in a thousand gets");
            sb.AppendLine("# one - the dev, and whoever else has asked to be in here. Set");
            sb.AppendLine("# VitalsSpecialOdds to 1 in FruitLab.ini if you want to see what they look");
            sb.AppendLine("# like; delete the names below if you would rather they never came up.");
            sb.AppendLine("[special]");
            foreach (var n in Special) sb.AppendLine(n);

            return sb.ToString();
        }

        private static readonly string[] Given =
        {
            "Adam",    "Adriana", "Ainsley", "Alexei",   "Amara",   "Anders",
            "Anneke",  "Arvid",   "Beatriz", "Bram",     "Callum",  "Camille",
            "Cassian", "Cecilia", "Dagny",   "Damaris",  "Dario",   "Delphine",
            "Dmitri",  "Edda",    "Eliot",   "Elspeth",  "Emeric",  "Esme",
            "Fabian",  "Fenna",   "Florian", "Gideon",   "Greta",   "Halvor",
            "Hester",  "Ines",    "Ivo",     "Jorunn",   "Junia",   "Kasimir",
            "Katrien", "Lazlo",   "Lenore",  "Lucien",   "Maarten", "Magda",
            "Marnix",  "Nadia",   "Nikolai", "Odile",    "Oskar",   "Petra",
            "Quinn",   "Rafael",  "Rikke",   "Rosalind", "Sander",  "Sibylle",
            "Silas",   "Tamsin",  "Theo",    "Ursula",   "Viggo",   "Wren",
        };

        private static readonly string[] Special =
        {
            "Tripledose", "Bob",
        };

        private static readonly string[] Family =
        {
            "Achterberg",  "Alderman",  "Barrow",      "Bellinger",  "Blackwood",
            "Boskamp",     "Brandt",    "Calloway",    "Castellan",  "Cordier",
            "Dehaan",      "Delacroix", "Dorlan",      "Draeger",    "Eberhardt",
            "Fairweather", "Felsen",    "Gallagher",   "Grieve",     "Halloran",
            "Havlicek",    "Hensley",   "Idris",       "Janszen",    "Kessler",
            "Kovacs",      "Lindqvist", "Lorne",       "Maartens",   "Merriweather",
            "Nachtegaal",  "Novak",     "Ostrand",     "Pelletier",  "Quimby",
            "Radek",       "Rasmussen", "Ravensworth", "Sable",      "Schuyler",
            "Sonnenfeld",  "Stenmark",  "Thackeray",   "Torvald",    "Underhill",
            "Vandermeer",  "Vasquez",   "Veldkamp",    "Voss",       "Waterhouse",
            "Wexler",      "Whitlock",  "Ysbrand",     "Zeller",
        };
    }
}
