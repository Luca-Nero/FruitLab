using Il2Cpp;
using Il2CppEffectors;
using MelonLoader;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    internal static class Diag
    {
        public static bool On => Config.LogDiagnostics;

        public static void Log(string channel, string message)
        {
            if (!On) return;
            MelonLogger.Msg($"[FruitLab:{channel}] f{Time.frameCount} {Time.time:0.000}  {message}");
        }

        public static string Name(LimbEffectorReceiver ler)
        {
            if (ler == null) return "<none>";
            try
            {
                var parent = ler.transform.parent;
                return parent != null ? parent.name : ler.name;
            }
            catch { return "<gone>"; }
        }

        public static string Name(Rigidbody rb)
        {
            if (rb == null) return "the world";
            try
            {
                var parent = rb.transform.parent;
                return parent != null ? parent.name : rb.name;
            }
            catch { return "<gone>"; }
        }

        public static string Name(Il2CppLVA.Limbs.LimbPhysics physics)
        {
            if (physics == null) return "<none>";
            try
            {
                var parent = physics.transform.parent;
                return parent != null ? parent.name : physics.name;
            }
            catch { return "<gone>"; }
        }

        // ── Limb wiring ───────────────────────────────────────────────────────

        public static void Wiring(string step, LimbEffectorReceiver ler)
        {
            if (!On) return;

            var refs = Limbs.RefsOf(ler);
            if (refs == null) { Log("wiring", $"{step}: {Name(ler)} has no references"); return; }

            try
            {
                var creature = Native.Creature(refs);
                var node     = Native.Node(refs);
                var puppet   = Native.Puppeteer(refs);

                string parent = "nothing";
                bool   native = false;

                if (node != null)
                {
                    native = Native.IsNodeNative(node);

                    if (Native.HasParentNode(node))
                    {
                        var up   = Native.ParentNode(node);
                        var limb = up != null ? Native.NodeLimb(up) : null;
                        parent   = limb != null ? LimbName(limb) : "something unnamed";
                    }
                }

                int layer = -1;
                try
                {
                    var col = ler.GetComponentInChildren<Collider>(true);
                    if (col != null) layer = col.gameObject.layer;
                }
                catch { }

                Log("wiring",
                    $"{step}: {Name(ler)} — body {Body(creature)}, " +
                    $"hangs off {parent}, native {native}, layer {layer}, " +
                    $"puppeteer {(puppet != null ? "yes" : "NO — it will hang limp")}");
            }
            catch (System.Exception e)
            {
                Log("wiring", $"{step}: could not read {Name(ler)}: {e.Message}");
            }
        }

        public static string Body(bam creature)
        {
            if (creature == null) return "none";
            try { return $"{creature.name}#{Limbs.CreatureId(creature)}"; }
            catch { return "<gone>"; }
        }

        public static void Survey(string label, List<LimbEffectorReceiver> limbs)
        {
            if (!On || limbs == null) return;

            Log("wiring", $"── {label}: {limbs.Count} limb(s) ──");
            foreach (var ler in limbs) Wiring(label, ler);
        }

        private static string LimbName(Il2CppLVA.Limbs.AbstractLimb limb)
        {
            try { return limb.transform.name; } catch { return "<gone>"; }
        }

        // ── Collision between two limbs ───────────────────────────────────────

        public static void Collision(string step, LimbEffectorReceiver a, LimbEffectorReceiver b)
        {
            if (!On || a == null || b == null) return;

            try
            {
                var mine   = a.GetComponentsInChildren<Collider>(true);
                var theirs = b.GetComponentsInChildren<Collider>(true);

                int pairs = 0, ignored = 0, disabled = 0;
                int layerA = -1, layerB = -1;

                foreach (var x in mine)
                {
                    if (x == null) continue;

                    layerA = x.gameObject.layer;
                    if (!x.enabled) disabled++;

                    foreach (var y in theirs)
                    {
                        if (y == null || y == x) continue;

                        layerB = y.gameObject.layer;
                        pairs++;

                        try { if (Physics.GetIgnoreCollision(x, y)) ignored++; } catch { }
                    }
                }

                bool layersOff = false;
                try
                {
                    if (layerA >= 0 && layerB >= 0)
                        layersOff = Physics.GetIgnoreLayerCollision(layerA, layerB);
                }
                catch { }

                string joint = "none";
                var ja = Limbs.JointOf(a);
                var jb = Limbs.JointOf(b);
                var rbA = Limbs.BodyOf(a);
                var rbB = Limbs.BodyOf(b);

                if (ja != null && ja.connectedBody != null && ja.connectedBody == rbB)
                    joint = $"{Name(a)} still jointed to {Name(b)} (collision {ja.enableCollision})";
                else if (jb != null && jb.connectedBody != null && jb.connectedBody == rbA)
                    joint = $"{Name(b)} still jointed to {Name(a)} (collision {jb.enableCollision})";

                Log("collision",
                    $"{step}: {Name(a)} vs {Name(b)} — {ignored}/{pairs} pair(s) excused, " +
                    $"{disabled} collider(s) off, layers {layerA}/{layerB}" +
                    (layersOff ? " EXCUSED" : "") + $", joint: {joint}");
            }
            catch (System.Exception e)
            {
                Log("collision", $"{step}: could not read: {e.Message}");
            }
        }

        // ── Body vitals ───────────────────────────────────────────────────────

        private static readonly List<Vitals.Handle> _params = new List<Vitals.Handle>();

        public static void Vitals(string step, LimbEffectorReceiver anyLimb)
        {
            if (!On || anyLimb == null) return;

            try
            {
                var creature = Limbs.CreatureOf(anyLimb);
                if (creature == null) { Log("vitals", $"{step}: no creature"); return; }

                var root = Limbs.CreatureRootOf(anyLimb);

                Log("vitals", $"{step}: {Body(creature)}, head {Limbs.HasHead(root)}");

                _params.Clear();
                FruitLab.Vitals.CollectCreature(creature, _params);
                Log("vitals", "  outputs: " + Describe(_params));

                _params.Clear();
                FruitLab.Vitals.CollectExternal(creature, "creature", _params);
                Log("vitals", "  inputs:  " + Describe(_params));
            }
            catch (System.Exception e)
            {
                Log("vitals", $"{step}: could not read: {e.Message}");
            }
        }

        private static string Describe(List<Vitals.Handle> handles)
        {
            var parts = new List<string>();

            foreach (var h in handles)
            {
                if (!h.Valid) continue;
                parts.Add($"{h.Key} {h.Fraction * 100f:0}%" + (h.Frozen ? " FROZEN" : ""));
            }

            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "none";
        }

        // ── Voxel watch ───────────────────────────────────────────────────────

        private sealed class Tracked
        {
            public LimbEffectorReceiver Ler;
            public string Name;
            public int    Last;
        }

        private static readonly List<Tracked> _tracked = new List<Tracked>();
        private static string _label = "watch";
        private static float  _until;

        public static void WatchVoxels(string label, List<LimbEffectorReceiver> limbs,
                                       float seconds)
        {
            _tracked.Clear();
            if (!On || limbs == null) return;

            _label = label;
            _until = Time.time + seconds;

            foreach (var ler in limbs)
            {
                if (ler == null) continue;
                _tracked.Add(new Tracked { Ler = ler, Name = Name(ler), Last = Destroyed(ler) });
            }

            Log(_label, $"watching {_tracked.Count} limb(s) for {seconds:0.0}s");
        }

        public static void Sample(string step, bool sayWhenNothing = true)
        {
            if (!On || _tracked.Count == 0) return;

            int changed = 0;

            foreach (var t in _tracked)
            {
                int now = Destroyed(t.Ler);
                if (now < 0 || now == t.Last) continue;

                int delta = now - t.Last;
                t.Last = now;
                changed++;

                Log(_label, delta > 0
                    ? $"{step}: {t.Name} LOST {delta} voxel(s) — {now} destroyed in total"
                    : $"{step}: {t.Name} regained {-delta} voxel(s) — {now} destroyed in total");
            }

            if (changed == 0 && sayWhenNothing) Log(_label, $"{step}: no change");
        }

        public static void Tick()
        {
            if (_tracked.Count == 0) return;

            if (Time.time > _until)
            {
                Log(_label, "watch ended");
                _tracked.Clear();
                return;
            }

            Sample("settling", sayWhenNothing: false);
        }

        public static void Stop() => _tracked.Clear();

        private static int Destroyed(LimbEffectorReceiver ler)
        {
            if (ler == null) return -1;
            try { return Limbs.DisabledVoxels(Limbs.ShapeOf(ler)); } catch { return -1; }
        }

        // ── Whole-body helpers ────────────────────────────────────────────────

        public static void Register()
        {
            Dismemberment.Split += ler =>
            {
                if (!On || _tracked.Count == 0) return;
                Log(_label, $"the game severed {Name(ler)}");
            };
        }
    }
}
