using FruitLib;
using Il2CppEffectors;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Vitals Monitor — a self-contained FruitLab item.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class VitalsMonitor
    {
        public const string ItemId      = "FruitLab:VitalsMonitor";
        public const string DisplayName = "Vitals Monitor";

        public static readonly Color IconColor = new Color(0.42f, 0.86f, 0.62f, 1f);

        private static readonly List<Reading> _pinned = new List<Reading>();
        private static Reading _aimed;
        private static bool    _equipped;

        private sealed class Reading
        {
            public Transform Anchor;
            public Transform CreatureRoot;

            public string    Name;
            public string    Record;
            public bool      Rare;

            public int       Rest  = 72;
            public int       Shift;

            public List<Vitals.Handle> Creature = new List<Vitals.Handle>();
            public List<Vitals.Handle> Limbs    = new List<Vitals.Handle>();
            public List<Vitals.Handle> Inputs   = new List<Vitals.Handle>();
            public List<Vitals.Handle> Organs   = new List<Vitals.Handle>();

            public float Age;
            public bool  Pinned;

            public float Mind = 1f;
            public float Blood = 1f;
            public int   Bpm;
            public float Phase;

            public int Frame = -1;

            public bool Alive => Anchor != null && CreatureRoot != null;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Toolbar item
        // ══════════════════════════════════════════════════════════════════════════

        public static void Equip() => _equipped = true;

        public static void Unequip()
        {
            _equipped = false;
            _aimed    = null;
        }

        public static void Click() => TogglePin();

        public static void OnSceneReload()
        {
            _equipped = false;
            _aimed    = null;
            _pinned.Clear();

            Patients.Reload();
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Tracking
        // ══════════════════════════════════════════════════════════════════════════

        public static void OnUpdate()
        {
            float dt = Time.deltaTime;

            for (int i = _pinned.Count - 1; i >= 0; i--)
            {
                var r = _pinned[i];
                if (!r.Alive) { _pinned.RemoveAt(i); continue; }
                Tick(r, dt);
            }

            if (!_equipped) { _aimed = null; return; }
            if (FruitMenu.BlocksGameplayInput) return;

            Transform root = Probe(out Transform anchor);
            if (root == null) _aimed = null;
            else if (_aimed == null || _aimed.CreatureRoot != root)
            {
                _aimed = Build(root, anchor);
            }
            else
            {
                _aimed.Anchor = anchor;
                Tick(_aimed, dt);
            }

        }

        private static void TogglePin()
        {
            if (_aimed == null) return;

            for (int i = 0; i < _pinned.Count; i++)
            {
                if (_pinned[i].CreatureRoot != _aimed.CreatureRoot) continue;
                _pinned.RemoveAt(i);
                MelonLogger.Msg("[FruitLab] Vitals unpinned.");
                return;
            }

            _aimed.Pinned = true;
            _pinned.Add(_aimed);
            MelonLogger.Msg($"[FruitLab] Vitals pinned on {_aimed.Name}.");
        }

        private static void Tick(Reading r, float dt)
        {
            if (r.Frame == Time.frameCount) return;
            r.Frame = Time.frameCount;

            Pulse(r, dt);
            Refresh(r, dt);
        }

        private static void Pulse(Reading r, float dt)
        {
            float mind = 1f, blood = 1f;

            foreach (var h in r.Creature)
            {
                if (!h.Valid) continue;
                if      (h.Key == KeyBlood) blood = h.Fraction;
                else if (h.Key == KeyMind)  mind  = h.Fraction;
            }

            r.Mind  = mind;
            r.Blood = blood;
            r.Bpm = mind <= 0.001f ? 0
                  : Mathf.RoundToInt(Mathf.Lerp(r.Rest + StressSpan, r.Rest, blood));

            if (r.Bpm > 0) r.Phase = Mathf.Repeat(r.Phase + r.Bpm / 60f * dt, 1f);
        }

        private static void Refresh(Reading r, float dt)
        {
            r.Age += dt;
            if (r.Age < 1f) return;
            r.Age = 0f;

            r.Creature.Clear();
            r.Limbs.Clear();
            r.Inputs.Clear();
            r.Organs.Clear();
            Collect(r);
        }

        private static void Collect(Reading r)
        {
            if (r.CreatureRoot == null) return;

            foreach (var ler in r.CreatureRoot.GetComponentsInChildren<LimbEffectorReceiver>(true))
            {
                if (ler == null) continue;

                if (r.Creature.Count == 0)
                {
                    var creature = Limbs.CreatureOf(ler);
                    if (creature != null)
                    {
                        Vitals.CollectCreature(creature, r.Creature);
                        Vitals.CollectExternal(creature, "creature", r.Inputs);
                    }
                }
                Vitals.CollectLimb(ler, r.Limbs);
                Vitals.CollectLimbExternal(ler, r.Inputs);
            }

            Vitals.CollectOrgans(r.CreatureRoot, r.Organs);
        }

        private static Transform Probe(out Transform anchor)
        {
            anchor = null;

            var cam = Camera.main;
            if (cam == null) return null;

            if (!Physics.Raycast(cam.transform.position, cam.transform.forward,
                                 out RaycastHit hit, Mathf.Max(Config.VitalsRange, 1f),
                                 ~0, QueryTriggerInteraction.Ignore))
                return null;

            if (hit.collider == null) return null;

            var ler = Limbs.Of(hit.collider.gameObject);
            if (ler == null) return null;

            anchor = ler.transform;
            return Limbs.CreatureRootOf(ler);
        }

        private static Reading Build(Transform root, Transform anchor)
        {
            var r = new Reading
            {
                Anchor       = anchor,
                CreatureRoot = root,
                Name         = Patients.NameFor(root),
                Record       = Patients.RecordFor(root),
                Rare         = Patients.IsRare(root),
                Rest         = Patients.RestingBpm(root),
                Shift        = Patients.PressureShift(root),
            };
            Collect(r);
            return r;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Panel
        // ══════════════════════════════════════════════════════════════════════════

        private const float PanelW   = 300f;
        private const float RowH     = 20f;
        private const float HeaderH  = 40f;
        private const float BarH     = 24f;
        private const float TraceH   = 40f;

        private const float TraceWindow = 2.6f;

        private const float StressSpan = 76f;

        public static void OnGUI()
        {
            if (_pinned.Count == 0 && _aimed == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            foreach (var r in _pinned) Draw(cam, r);

            if (_aimed == null || _aimed.Pinned) return;
            bool dup = false;
            foreach (var p in _pinned) if (p.CreatureRoot == _aimed.CreatureRoot) { dup = true; break; }
            if (!dup) Draw(cam, _aimed);
        }

        private static void Draw(Camera cam, Reading r)
        {
            if (!r.Alive) return;

            Vector3 world  = r.Anchor.position + Vector3.up * 0.55f;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;

            float mind = r.Mind, bloodFrac = r.Blood;

            bool dead = mind <= 0.001f;

            string state = dead         ? "DECEASED"
                         : mind < 0.25f ? "UNCONSCIOUS"
                         : mind < 0.95f ? "IMPAIRED" : "ALIVE";
            Color stateColor = dead || mind < 0.25f ? FruitLabHud.Bad
                             : mind < 0.95f         ? FruitLabHud.Warn : FruitLabHud.Good;

            float sustain  = LazarusSyringe.SustainOn(r.CreatureRoot);
            bool  treating = HealingSyringe.TreatingOn(r.CreatureRoot);
            bool  propped  = sustain > 0f;

            bool raw = Config.VitalsRaw;

            int rawRows = raw ? r.Creature.Count + LimbKeys(r).Count
                              + InputKeys(r).Count + OrganKeys(r).Count + 2 : 0;

            int notes = (propped ? 1 : 0) + (treating ? 1 : 0);

            float h = HeaderH + BarH * 2f + TraceH + RowH * 2f + 16f + rawRows * RowH
                    + (notes > 0 ? notes * RowH + 6f : 0f);
            float x = screen.x + 26f;
            float y = Screen.height - screen.y - h * 0.5f;

            x = Mathf.Clamp(x, 4f, Screen.width  - PanelW - 4f);
            y = Mathf.Clamp(y, 4f, Screen.height - h      - 4f);

            var panel = new Rect(x, y, PanelW, h);
            Fill(panel, new Color(0.05f, 0.06f, 0.07f, 0.88f));
            Fill(new Rect(panel.x, panel.y, panel.width, 2f), IconColor);

            float cy = panel.y + 5f;
            Label(new Rect(panel.x + 8f, cy, PanelW - 92f, RowH), r.Name,
                  r.Rare ? FruitLabHud.Rare : FruitLabHud.Normal, 13);
            Label(new Rect(panel.x + PanelW - 84f, cy, 76f, RowH), state, stateColor, 12);

            Label(new Rect(panel.x + 8f, cy + 16f, PanelW - 16f, RowH),
                  $"REC {r.Record}", FruitLabHud.Dim, 10);

            cy = panel.y + HeaderH;

            // ── The two that matter ───────────────────────────────────────────
            Gauge(panel.x, cy, "CONSCIOUSNESS", mind, dead, propped);
            cy += BarH;
            Gauge(panel.x, cy, "BLOOD", bloodFrac, dead, propped);
            cy += BarH + 2f;

            // ── Treatment ─────────────────────────────────────────────────────
            if (propped)
            {
                float span = Mathf.Max(Config.LazarusDuration, 0.01f);

                Label(new Rect(panel.x + 8f, cy, 150f, RowH), "LIFE SUPPORT",
                      FruitLabHud.Held, 11);
                Label(new Rect(panel.x + PanelW - 60f, cy, 52f, RowH),
                      Mathf.CeilToInt(sustain) + "s", FruitLabHud.Held, 11);

                var run = new Rect(panel.x + 8f, cy + RowH - 4f, PanelW - 16f, 3f);
                Fill(run, new Color(1f, 1f, 1f, 0.08f));
                Fill(new Rect(run.x, run.y, run.width * Mathf.Clamp01(sustain / span), run.height),
                     FruitLabHud.Held);

                cy += RowH;
            }

            if (treating)
            {
                Label(new Rect(panel.x + 8f, cy, PanelW - 16f, RowH),
                      "TREATING — healing syringe", FruitLabHud.Good, 11);
                cy += RowH;
            }

            if (notes > 0) cy += 6f;

            // ── Trace ─────────────────────────────────────────────────────────
            int bpm = r.Bpm;
            int sys = dead ? 0 : Mathf.RoundToInt(Mathf.Lerp(58f, 122f + r.Shift,          bloodFrac));
            int dia = dead ? 0 : Mathf.RoundToInt(Mathf.Lerp(34f,  78f + r.Shift * 0.6f,   bloodFrac));

            Trace(new Rect(panel.x + 8f, cy, PanelW - 16f, TraceH), bpm, r.Phase, bloodFrac, dead);
            cy += TraceH + 4f;

            Label(new Rect(panel.x + 8f, cy, PanelW - 16f, RowH),
                  dead ? "0 bpm    0/0 mmHg" : $"~{bpm} bpm    ~{sys}/{dia} mmHg",
                  dead ? FruitLabHud.Bad : FruitLabHud.Normal, 12);
            cy += RowH;

            Label(new Rect(panel.x + 8f, cy, PanelW - 16f, RowH),
                  r.Pinned ? "pinned — click again to release" : "estimated from blood volume",
                  FruitLabHud.Dim, 10);
            cy += RowH + 2f;

            if (!raw) return;

            // ── Everything else, for when a number is what you want ───────────
            foreach (var hnd in r.Creature)
            {
                if (!hnd.Valid || Vitals.IsPosture(hnd.Key)) continue;
                Row(panel.x, cy, Friendly(hnd.Key), hnd.Key, hnd.Fraction, hnd.Frozen);
                cy += RowH;
            }

            foreach (var key in OrganKeys(r))
            {
                float worst = Worst(r.Organs, key, out string where, out bool held);
                Row(panel.x, cy, "organ " + key, where, worst, held);
                cy += RowH;
            }

            foreach (var key in InputKeys(r))
            {
                if (Vitals.IsPosture(key)) continue;
                float worst = Worst(r.Inputs, key, out string where, out bool held);
                Row(panel.x, cy, Friendly(key), where, worst, held);
                cy += RowH;
            }

            cy += 2f;
            Label(new Rect(panel.x + 8f, cy, PanelW - 16f, RowH),
                  "posture — not a health readout", FruitLabHud.Dim, 10);
            cy += RowH - 3f;

            foreach (var hnd in r.Creature)
            {
                if (!hnd.Valid || !Vitals.IsPosture(hnd.Key)) continue;
                Row(panel.x, cy, Friendly(hnd.Key), hnd.Key, hnd.Fraction, hnd.Frozen, true);
                cy += RowH;
            }

            foreach (var key in LimbKeys(r))
            {
                float worst = Worst(r.Limbs, key, out string where, out bool held);
                Row(panel.x, cy, "limb " + key, where, worst, held, Vitals.IsPosture(key));
                cy += RowH;
            }

            foreach (var key in InputKeys(r))
            {
                if (!Vitals.IsPosture(key)) continue;
                float worst = Worst(r.Inputs, key, out string where, out bool held);
                Row(panel.x, cy, Friendly(key), where, worst, held, true);
                cy += RowH;
            }
        }

        private static void Gauge(float px, float y, string name, float frac, bool dead,
                                 bool held = false)
        {
            frac = Mathf.Clamp01(frac);

            Label(new Rect(px + 8f, y, 140f, RowH), name, FruitLabHud.Dim, 10);
            Label(new Rect(px + PanelW - 52f, y, 44f, RowH),
                  Mathf.RoundToInt(frac * 100f) + "%",
                  dead ? FruitLabHud.Bad : FruitLabHud.Normal, 12);

            var bar = new Rect(px + 8f, y + RowH - 3f, PanelW - 16f, 7f);
            Fill(bar, new Color(1f, 1f, 1f, 0.08f));
            Fill(new Rect(bar.x, bar.y, bar.width * frac, bar.height),
                 held             ? FruitLabHud.Held
                 : dead           ? FruitLabHud.Bad
                 : frac < 0.25f   ? FruitLabHud.Bad
                 : frac < 0.6f    ? FruitLabHud.Warn : FruitLabHud.Good);
        }

        private static void Trace(Rect box, int bpm, float phase, float blood, bool dead)
        {
            Fill(box, new Color(1f, 1f, 1f, 0.05f));

            if (Event.current != null && Event.current.type != EventType.Repaint) return;

            float mid  = box.y + box.height * 0.5f;
            var   line = dead ? FruitLabHud.Bad : FruitLabHud.Good;

            if (dead)
            {
                Fill(new Rect(box.x, mid, box.width, 1f), line);
                return;
            }

            float cycles = Mathf.Max(TraceWindow * bpm / 60f, 1f);
            float amp = box.height * 0.42f * Mathf.Clamp01(0.35f + blood * 0.65f);

            const float step = 2f;
            float prev = mid;

            for (float dx = 0f; dx < box.width; dx += step)
            {
                float at   = Mathf.Repeat(phase + (box.width - dx) / box.width * cycles, 1f);
                float here = mid - Beat(at) * amp;

                float top = Mathf.Min(prev, here);
                Fill(new Rect(box.x + dx, top, step, Mathf.Max(Mathf.Abs(here - prev), 1f)), line);

                prev = here;
            }
        }

        private static float Beat(float p)
        {
            float v = 0f;
            v += 0.16f * Bump(p, 0.16f, 0.030f);
            v -= 0.18f * Bump(p, 0.28f, 0.008f);
            v += 1.00f * Bump(p, 0.31f, 0.008f);
            v -= 0.30f * Bump(p, 0.34f, 0.010f);
            v += 0.26f * Bump(p, 0.52f, 0.045f);
            return v;
        }

        private static float Bump(float p, float at, float width)
        {
            float d = (p - at) / width;
            return Mathf.Exp(-d * d);
        }

        private static void Row(float px, float y, string name, string key, float frac,
                               bool frozen, bool posture = false)
        {
            Label(new Rect(px + 8f, y, 104f, RowH), name,
                  posture ? FruitLabHud.Dim : FruitLabHud.Normal, 11);
            Label(new Rect(px + 112f, y, 34f, RowH), Mathf.RoundToInt(frac * 100f) + "%",
                  FruitLabHud.Dim, 11);

            var bar = new Rect(px + 148f, y + 5f, 62f, 6f);
            Fill(bar, new Color(1f, 1f, 1f, 0.10f));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(frac), bar.height),
                 frozen  ? FruitLabHud.Held
                 : posture ? FruitLabHud.Posture
                 : frac <= 0.001f ? FruitLabHud.Bad
                 : frac < 0.5f    ? FruitLabHud.Warn : FruitLabHud.Good);

            Label(new Rect(px + 214f, y, 30f, RowH), frozen ? "HELD" : key, FruitLabHud.Dim, 9);
        }

        private const string KeyBlood   = "bbl";
        private const string KeyMind    = "bbj";
        private const string KeyBalance = "bbh";
        private const string KeyMuscle  = "bbk";

        private static string Friendly(string key)
        {
            switch (key)
            {
                case KeyBlood:   return "blood";
                case KeyMind:    return "consciousness";
                case KeyBalance: return "balance";
                case KeyMuscle:  return "muscle";
                default:         return key;
            }
        }

        private static List<string> LimbKeys(Reading r)
        {
            _limbKeys.Clear();
            foreach (var hnd in r.Limbs)
                if (hnd.Valid && !_limbKeys.Contains(hnd.Key)) _limbKeys.Add(hnd.Key);
            _limbKeys.Sort(StringComparer.Ordinal);
            return _limbKeys;
        }

        private static readonly List<string> _limbKeys = new List<string>();

        private static List<string> InputKeys(Reading r)
        {
            _inputKeys.Clear();
            foreach (var hnd in r.Inputs)
                if (hnd.Valid && !_inputKeys.Contains(hnd.Key)) _inputKeys.Add(hnd.Key);
            _inputKeys.Sort(StringComparer.Ordinal);
            return _inputKeys;
        }

        private static readonly List<string> _inputKeys = new List<string>();

        private static List<string> OrganKeys(Reading r)
        {
            _organKeys.Clear();
            foreach (var hnd in r.Organs)
                if (hnd.Valid && !_organKeys.Contains(hnd.Key)) _organKeys.Add(hnd.Key);
            _organKeys.Sort(StringComparer.Ordinal);
            return _organKeys;
        }

        private static readonly List<string> _organKeys = new List<string>();

        private static float Worst(List<Vitals.Handle> set, string key,
                                   out string where, out bool held)
        {
            where = key;
            held  = false;

            float worst = float.MaxValue, best = float.MinValue;
            string owner = null;
            int count = 0;

            foreach (var hnd in set)
            {
                if (!hnd.Valid || hnd.Key != key) continue;
                float f = hnd.Fraction;
                count++;
                if (f > best) best = f;
                if (f < worst) { worst = f; owner = hnd.Owner; held = hnd.Frozen; }
            }

            if (count == 0) return 1f;

            where = (count > 1 && best - worst <= 0.005f) ? "all" : Short(owner ?? key);
            return worst;
        }

        private static string Short(string owner)
        {
            string n = owner.Replace("Prefab(Clone)", "").Replace("(Clone)", "");
            return n.Length > 9 ? n.Substring(0, 9) : n;
        }

        // ── Drawing primitives ────────────────────────────────────────────────

        private static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void Label(Rect r, string text, Color c, int size)
        {
            var style = FruitLabHud.Text(size);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }
    }
}
