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
    //
    // A read-only status panel that floats beside a body: what the LVA graph actually
    // thinks of it, plus the clinical numbers you would expect on a monitor. Aim at a
    // creature while the slot is held to read it; left click pins it so it keeps
    // reading while you switch to something that does damage.
    //
    // Written as much for building the rest of FruitLab as for playing with. Every
    // question the Lazarus Syringe raised — which parameter is consciousness, what
    // the solver does to a value after a dose expires, whether a "revived" body is
    // genuinely whole — is a thing you can now watch instead of infer from a log.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class VitalsMonitor
    {
        public const string ItemId = "FruitLab:VitalsMonitor";

        private static readonly Color IconColor = new Color(0.42f, 0.86f, 0.62f, 1f);

        private static readonly List<Reading> _pinned = new List<Reading>();
        private static Reading _aimed;
        private static bool    _equipped;

        /// One body being read. Handles are collected once and reused; the values
        /// behind them are live, so only the parameter set needs refreshing (a limb
        /// coming off changes it).
        private sealed class Reading
        {
            public Transform Anchor;        // a limb, so the panel tracks the body
            public Transform CreatureRoot;
            public string    Name;

            public List<Vitals.Handle> Creature = new List<Vitals.Handle>();
            public List<Vitals.Handle> Limbs    = new List<Vitals.Handle>();
            public List<Vitals.Handle> Inputs   = new List<Vitals.Handle>();
            public List<Vitals.Handle> Organs   = new List<Vitals.Handle>();

            public float Age;               // seconds since the set was collected
            public bool  Pinned;

            public bool Alive => Anchor != null && CreatureRoot != null;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Toolbar item
        // ══════════════════════════════════════════════════════════════════════════

        public static void Register()
        {
            FruitToolbar.Register(new FruitToolbarItem
            {
                Id           = ItemId,
                Name         = "Vitals Monitor",
                Icon         = FruitToolbar.MakeSolidIcon(IconColor),
                OnSelected   = slot => _equipped = true,
                OnDeselected = slot => { _equipped = false; _aimed = null; },
            });
        }

        public static void OnSceneReload()
        {
            _equipped = false;
            _aimed    = null;
            _pinned.Clear();
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
                Refresh(r, dt);
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
                // Same body as last frame: keep its parameter set and just follow the
                // limb being pointed at. Re-collecting per frame would walk every
                // entity on the creature sixty times a second for no new information.
                _aimed.Anchor = anchor;
                Refresh(_aimed, dt);
            }

            if (Input.GetMouseButtonDown(0)) TogglePin();
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

        /// Rebuilds a reading's parameter set on a slow cadence — losing a limb
        /// changes what there is to read, and a stale handle points at a dead object.
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

        /// The creature root the player is looking at, or null. Cheap — one raycast
        /// and two lookups, no parameter collection.
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
                Name         = root.name.Replace("(Clone)", "").Replace("Prefab", ""),
            };
            Collect(r);
            return r;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Panel
        // ══════════════════════════════════════════════════════════════════════════

        private const float PanelW = 246f;
        private const float RowH   = 17f;

        public static void OnGUI()
        {
            if (_pinned.Count == 0 && _aimed == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            foreach (var r in _pinned) Draw(cam, r);

            // Skip the aimed panel when that body is already pinned, or it draws twice.
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
            if (screen.z <= 0f) return;   // behind the camera

            int rows = r.Creature.Count + LimbKeys(r).Count
                     + InputKeys(r).Count + OrganKeys(r).Count + 6;
            float h  = 26f + rows * RowH + 10f;
            float x  = screen.x + 26f;
            float y  = Screen.height - screen.y - h * 0.5f;

            x = Mathf.Clamp(x, 4f, Screen.width  - PanelW - 4f);
            y = Mathf.Clamp(y, 4f, Screen.height - h      - 4f);

            var panel = new Rect(x, y, PanelW, h);
            Fill(panel, new Color(0.05f, 0.06f, 0.07f, 0.82f));
            Fill(new Rect(panel.x, panel.y, panel.width, 2f), IconColor);

            float bloodFrac = 1f, mind = 1f;
            foreach (var hnd in r.Creature)
            {
                if (!hnd.Valid) continue;
                if      (hnd.Key == KeyBlood) bloodFrac = hnd.Fraction;
                else if (hnd.Key == KeyMind)  mind      = hnd.Fraction;
            }

            // Death is consciousness bottoming out and nothing else — CrunchedDeathCounter
            // watches that one parameter.
            //
            // Balance used to count towards this and does not any more. A perfectly
            // healthy body lifted off the ground reads zero balance, and calling that
            // IMPAIRED was the panel reporting posture as injury.
            bool dead = mind <= 0.001f;

            string state = dead              ? "DECEASED"
                         : mind < 0.25f      ? "UNCONSCIOUS"
                         : mind < 0.95f      ? "IMPAIRED" : "ALIVE";
            Color stateColor = dead || mind < 0.25f ? FruitLabHud.Bad
                             : mind < 0.95f         ? FruitLabHud.Warn : FruitLabHud.Good;

            float cy = panel.y + 6f;
            Label(new Rect(panel.x + 8f, cy, PanelW - 16f, RowH), r.Name, FruitLabHud.Normal, 12);
            Label(new Rect(panel.x + PanelW - 84f, cy, 76f, RowH), state, stateColor, 12);
            cy += RowH + 6f;

            // ── Vitals: what the body has lost and can be given back ──────────
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

            // ── Posture: how it is carrying itself right now ───────────────────
            //
            // Kept apart and drawn flat, because these are not a health readout and
            // colouring them like one is actively misleading: a pristine ragdoll held in
            // the air reads muscle 5%, balance 0%, zr 5%. Low here means "limp", not
            // "hurt", and it changes second to second.
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
            cy += 4f;

            // Clinical numbers, derived from blood rather than read from the game —
            // the game models no such thing, so they are flavour with an honest label.
            int bpm = dead ? 0 : Mathf.RoundToInt(Mathf.Lerp(148f, 72f, bloodFrac)
                                                  + Mathf.Sin(Time.time * 3f) * 2f);
            int sys = dead ? 0 : Mathf.RoundToInt(Mathf.Lerp(58f, 122f, bloodFrac));
            int dia = dead ? 0 : Mathf.RoundToInt(Mathf.Lerp(34f, 78f, bloodFrac));

            Label(new Rect(panel.x + 8f, cy, PanelW - 16f, RowH),
                  $"~{bpm} bpm   ~{sys}/{dia} mmHg",
                  dead ? FruitLabHud.Bad : FruitLabHud.Normal, 11);
            cy += RowH;
            Label(new Rect(panel.x + 8f, cy, PanelW - 16f, RowH),
                  r.Pinned ? "pinned — click again to release" : "estimated from blood volume",
                  FruitLabHud.Dim, 10);
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
                 : posture ? FruitLabHud.Posture          // flat: low means limp, not hurt
                 : frac <= 0.001f ? FruitLabHud.Bad
                 : frac < 0.5f    ? FruitLabHud.Warn : FruitLabHud.Good);

            // The raw parameter key stays on screen on purpose: the friendly names are
            // inferred, and this is the column that lets you check them.
            Label(new Rect(px + 214f, y, 30f, RowH), frozen ? "HELD" : key, FruitLabHud.Dim, 9);
        }

        // All four confirmed by watching one chest wound play out, 2026-08-29:
        //   bbl blood         BloodTank points at it; keeps draining after death
        //   bbj consciousness the kill registers the instant it reaches 0
        //   bbh balance       tracks the worst limb value; at 0 the body cannot stand
        //   bbk muscle        snaps 100 to 5 and back in time with each attempt to rise
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

        /// Distinct limb parameter types on this body, in a stable order.
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

        /// Worst value for one parameter type, and who owns it.
        ///
        /// Reports "all" when every owner is on the same value rather than naming one of
        /// them. Several of these parameters are creature-wide — every limb carries an
        /// identical figure — and naming whichever happened to be enumerated first read
        /// as a finding about that limb. Shooting a head and being told the pelvis was
        /// worst was the display lying, not the game being strange.
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

    /// <summary>
    /// Minimal IMGUI helpers for FruitLab's own panels.
    ///
    /// Deliberately never touches <c>font</c>, <c>fontStyle</c> or <c>alignment</c>:
    /// those live in UnityEngine.TextRenderingModule, whose JIT type resolution fails
    /// on a half-generated Il2CppAssemblies dump in a way a local try/catch cannot
    /// catch. FruitLib quarantines them behind one no-inline method for that reason;
    /// FruitLab sidesteps the module entirely and lays out with explicit rects.
    /// </summary>
    internal static class FruitLabHud
    {
        public static readonly Color Normal = new Color(0.92f, 0.94f, 0.95f, 1f);
        public static readonly Color Dim    = new Color(0.55f, 0.58f, 0.60f, 1f);
        public static readonly Color Good   = new Color(0.45f, 0.85f, 0.45f, 1f);
        public static readonly Color Warn   = new Color(0.95f, 0.75f, 0.30f, 1f);
        public static readonly Color Bad    = new Color(0.90f, 0.40f, 0.40f, 1f);
        public static readonly Color Held   = new Color(0.45f, 0.72f, 1f,    1f);
        /// Posture bars are drawn flat — a low one means limp, not injured.
        public static readonly Color Posture = new Color(0.58f, 0.60f, 0.64f, 1f);

        private static readonly Dictionary<int, GUIStyle> _bySize = new Dictionary<int, GUIStyle>();

        public static GUIStyle Text(int size)
        {
            if (_bySize.TryGetValue(size, out var s) && s != null) return s;

            s = new GUIStyle(GUI.skin.label) { fontSize = size };
            _bySize[size] = s;
            return s;
        }

        /// GUI.skin is per-scene, so cached styles built against the old one go stale.
        public static void Reset() => _bySize.Clear();
    }
}
