using Il2Cpp;
using Il2CppEffectors;
using MelonLoader;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    /// <summary>
    /// Runtime instrumentation, so a report can say *what happened* instead of what it
    /// looked like.
    ///
    /// The centrepiece is the voxel watch. Destroyed voxels are the one damage readout
    /// this game hands over cheaply — <c>DisabledVoxelsCount</c>, a plain int per limb —
    /// so counting them before and after each step of an operation says exactly which
    /// step destroyed flesh, and in which limb. Sampling once a frame would only narrow
    /// it to the frame, and a suture does everything it does inside one; <see cref="Sample"/>
    /// is therefore called *between* steps, synchronously, and the log reads as a
    /// sequence rather than a snapshot.
    ///
    /// Everything here is behind <c>LogDiagnostics</c> and costs a bool read when off.
    /// </summary>
    internal static class Diag
    {
        public static bool On => Config.LogDiagnostics;

        public static void Log(string channel, string message)
        {
            if (!On) return;
            MelonLogger.Msg($"[FruitLab:{channel}] f{Time.frameCount} {Time.time:0.000}  {message}");
        }

        /// The limb prefab's name. The receiver lives on a "Physics" child, so its own
        /// name says nothing — the parent is the one that reads as "LeftLegPrefab".
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

        /// A rigidbody's limb, by the same naming. Rigidbodies sit on the "Physics"
        /// node, so their own names are all identical and say nothing.
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

        /// What a limb is currently attached to, and whether anything is animating it.
        ///
        /// The puppeteer readout is the one that matters for a suture: a limb with no
        /// puppeteer hangs, however well it is bolted on, because nothing is driving
        /// its muscles. Logged either side of the graft, that says whether the attach
        /// protocol reached the listeners or only moved the node.
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

                // By identity, not by name. A severed limb gets a creature of its own
                // built from the same prefab, so two completely different bodies both
                // read as "HumanCreaturePrefab(Clone)" — which hid a call being made
                // against the wrong hierarchy entirely.
                Log("wiring",
                    $"{step}: {Name(ler)} — body {Body(creature)}, " +
                    $"hangs off {parent}, native {native}, " +
                    $"puppeteer {(puppet != null ? "yes" : "NO — it will hang limp")}");
            }
            catch (System.Exception e)
            {
                Log("wiring", $"{step}: could not read {Name(ler)}: {e.Message}");
            }
        }

        /// A creature, named *and* numbered. Names alone are ambiguous here.
        public static string Body(bam creature)
        {
            if (creature == null) return "none";
            try { return $"{creature.name}#{Limbs.CreatureId(creature)}"; }
            catch { return "<gone>"; }
        }

        /// One wiring line per limb, for a whole body at once.
        ///
        /// The control that should have been run first. "Puppeteer NO" on a sutured limb
        /// only means something if an *intact* limb on the same body says yes — and if
        /// none of them do, the puppeteer was never what holds a limb up here and the
        /// whole line of attack was aimed at the wrong system.
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

        // ── Body vitals ───────────────────────────────────────────────────────

        private static readonly List<Vitals.Handle> _params = new List<Vitals.Handle>();

        /// A body's own top-level parameters on one line, with whether it has a head.
        ///
        /// Consciousness is the one that decides whether anything is home. A body given
        /// a head back that stands up rigid and refuses to walk is either not reading
        /// the new head at all, or was latched as dead when it lost the old one — and
        /// those look identical from outside but not here.
        public static void Vitals(string step, LimbEffectorReceiver anyLimb)
        {
            if (!On || anyLimb == null) return;

            try
            {
                var creature = Limbs.CreatureOf(anyLimb);
                if (creature == null) { Log("vitals", $"{step}: no creature"); return; }

                var root = Limbs.CreatureRootOf(anyLimb);

                Log("vitals", $"{step}: {Body(creature)}, head {Limbs.HasHead(root)}");

                // Outputs and inputs, separately, and marked when frozen.
                //
                // Internals are what the solver computes; externals are what it computes
                // them *from*. A parameter stuck at a value is either being held there by
                // us — the Lazarus freeze is a real possibility and would be our bug — or
                // driven there by an input, and only these two lines apart can say which.
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

        /// Starts counting destroyed voxels on a set of limbs.
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

        /// Reads every watched limb right now and reports what changed since the last
        /// read. Call it between steps — that is what makes the log a sequence.
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

        /// Reports severing while a watch is running — the tail end of most of the ways
        /// a limb operation goes wrong, and worth seeing next to the voxel counts.
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
