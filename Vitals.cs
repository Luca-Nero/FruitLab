using HarmonyLib;
using Il2Cpp;
using Il2CppEffectors;
using Il2CppInterop.Runtime;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    /// <summary>
    /// Shared access to the LVA parameters that decide whether a creature can still
    /// function — blood, cognition, muscle force, pain — separate from
    /// <see cref="Limbs"/>, which deals in voxels and damage signals.
    ///
    /// Restoring targets each parameter's <b>MaxValue</b>.
    ///
    /// It targeted <c>initialValue</c> first, on the theory that it held the spawn
    /// state. It does not — measured in-game, it is the value the LimitedValue was
    /// constructed with, which is 0 for most parameters; LVA raises them to their max
    /// while the creature initialises. Restoring to it wrote zero into everything and
    /// killed the body outright. On a pristine creature every parameter in this build
    /// sits at max, so max is what "alive" means here.
    ///
    /// That does assume no parameter in the set is pain-like, where max would be the
    /// bad end. None is in v0.1. If a later version adds one, this is where it breaks,
    /// and LogVitals on a healthy creature is how you would spot it — anything not
    /// sitting near its max when the body is fine does not belong here.
    ///
    /// The dependency solver owns these values and recomputes them from organ state.
    /// While the organs are intact it agrees that the creature is alive and a restore
    /// simply holds; once they are destroyed it keeps computing "dead" and drags the
    /// values back down, and re-applying on a cadence turns into an arm-wrestle at the
    /// tick rate. In-game that reads as the body convulsing between alive and dead
    /// several times a second.
    ///
    /// So vitals are <b>frozen</b> rather than re-applied: pinned once to their max,
    /// then held there by blocking every other write to them (see
    /// <see cref="PatchLimitedValueSetValue"/>). Nothing is left to oscillate against.
    /// </summary>
    internal static class Vitals
    {
        /// One restorable parameter, already resolved to its LimitedValue base.
        internal readonly struct Handle
        {
            public readonly bjq  Param;
            public readonly string Label;

            public Handle(bjq param, string label) { Param = param; Label = label; }
            public bool Valid => Param != null;
        }

        // ── Collection ────────────────────────────────────────────────────────

        /// Every internal parameter on one LVA entity, walked straight out of the
        /// module's dictionary.
        ///
        /// The public parameters API was the obvious route and does not work: its
        /// lookups are generic methods on an IL2CPP interface, and generic-instance
        /// virtual dispatch through one silently resolves nothing here — it returned
        /// empty for all 34 parameters of a creature that plainly had them, in every
        /// state tested. The dictionary needs no generic dispatch, no cast back from
        /// a readonly wrapper, and no hardcoded list of parameter types, so it also
        /// picks up the extra parameters later game versions added for free.
        public static int Collect(bcx entity, string prefix, List<Handle> into)
        {
            if (entity == null) return 0;

            Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Type, bdb> map;
            try
            {
                var module = Native.ParametersModule(entity);
                if (module == null) return 0;
                map = Native.ParameterMap(module);
                if (map == null) return 0;
            }
            catch { return 0; }

            int found = 0;
            try
            {
                var e = map.GetEnumerator();
                while (e.MoveNext())
                {
                    var kv = e.Current;
                    bdb param = kv.Value;
                    if (param == null) continue;

                    // A parameter derives from LimitedValue in v0.1, so it already is
                    // the value object — no cast needed. The key is the parameter's
                    // own Type, which makes a far better label than a guessed name.
                    string name;
                    try { name = kv.Key != null ? kv.Key.Name : "?"; } catch { name = "?"; }

                    into.Add(new Handle(param, prefix + "/" + name));
                    found++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FruitLab] vitals walk failed on {prefix}: {ex.Message}");
            }

            // Report the dictionary's own count when nothing came out, so an empty
            // module is distinguishable from a walk that could not read a full one.
            if (found == 0)
            {
                int size = -1;
                try { size = map.Count; } catch { }
                MelonLogger.Warning($"[FruitLab] no vitals on {prefix} (module reports {size} entries)");
            }

            return found;
        }

        /// The creature's own parameters — blood, and whatever else sits at
        /// creature level.
        public static int CollectCreature(bcx creature, List<Handle> into) =>
            Collect(creature, "creature", into);

        /// One limb's parameters. These drive muscle force, so a body with full
        /// blood and dead limb parameters still will not move.
        public static int CollectLimb(LimbEffectorReceiver ler, List<Handle> into)
        {
            bcx entity;
            try
            {
                var refs = ler.m_limbReferences;
                if (refs == null) return 0;
                entity = Native.LimbEntity(refs);
            }
            catch { return 0; }

            string label = ler.transform.parent != null ? ler.transform.parent.name : ler.name;
            return Collect(entity, label, into);
        }

        // ── Restore ───────────────────────────────────────────────────────────

        /// Pushes one parameter back up to its maximum, but only if it has actually
        /// sagged. The check is a plain field read while the write fires a full
        /// dependency solve, so skipping no-op writes is most of the cost saved on a
        /// body that is holding steady.
        ///
        /// The deadband is relative because the parameters are on wildly different
        /// scales — limb values run 0–100, blood runs 0–5146 — and a healthy value
        /// hovers a hair under max rather than sitting exactly on it.
        public static bool Restore(in Handle h)
        {
            if (!h.Valid) return false;

            try
            {
                float max = Native.Max(h.Param);
                float deadband = Math.Max(0.01f, Math.Abs(max) * 0.005f);
                if (Native.Value(h.Param) >= max - deadband) return false;

                Native.SetValue(h.Param, max);
                return true;
            }
            catch { return false; }
        }

        /// Restores a whole set, returning how many actually needed writing. With the
        /// set frozen this is a no-op; it stays as the fallback for a build where the
        /// freeze patch did not take, where it degrades to the old re-apply behaviour.
        public static int RestoreAll(List<Handle> handles)
        {
            _selfWriting = true;
            try
            {
                int written = 0;
                foreach (var h in handles) if (Restore(h)) written++;
                return written;
            }
            finally { _selfWriting = false; }
        }

        // ── Freeze ────────────────────────────────────────────────────────────

        /// Parameters currently pinned, by native pointer — wrapper references are
        /// not identity here, the same native object can arrive as different managed
        /// wrappers. Refcounted so two doses on one creature do not release each
        /// other's hold.
        private static readonly Dictionary<IntPtr, int> _frozen = new Dictionary<IntPtr, int>();
        private static bool _selfWriting;

        public static bool AnyFrozen => _frozen.Count > 0;

        /// Pins each parameter at its maximum and holds it there.
        public static void Freeze(List<Handle> handles)
        {
            _selfWriting = true;
            try
            {
                foreach (var h in handles)
                {
                    if (!h.Valid) continue;
                    try
                    {
                        Native.SetValue(h.Param, Native.Max(h.Param));

                        IntPtr key = IL2CPP.Il2CppObjectBaseToPtr(h.Param);
                        _frozen[key] = _frozen.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                    catch { }
                }
            }
            finally { _selfWriting = false; }
        }

        /// Hands the parameters back to the solver.
        public static void Unfreeze(List<Handle> handles)
        {
            foreach (var h in handles)
            {
                if (!h.Valid) continue;
                try
                {
                    IntPtr key = IL2CPP.Il2CppObjectBaseToPtr(h.Param);
                    if (!_frozen.TryGetValue(key, out int n)) continue;
                    if (n <= 1) _frozen.Remove(key); else _frozen[key] = n - 1;
                }
                catch { }
            }
        }

        /// Scene changes destroy every creature we were holding, so the whole ledger
        /// goes with them — a stale entry would pin a parameter on a fresh creature
        /// that happened to land on the same address.
        public static void UnfreezeAll() => _frozen.Clear();

        /// True when this write should be refused. Kept as cheap as possible: it sits
        /// on a method the game calls constantly, so the common case is one int
        /// compare against an empty ledger.
        internal static bool BlocksWrite(bjq value)
        {
            if (_frozen.Count == 0 || _selfWriting || value == null) return false;
            try { return _frozen.ContainsKey(IL2CPP.Il2CppObjectBaseToPtr(value)); }
            catch { return false; }
        }

        // ── Diagnostics ───────────────────────────────────────────────────────

        /// Dumps value / initial / min / max for a set. Off by default; turn on
        /// LogVitals when a creature will not stay alive and you need to see which
        /// parameter the solver is dragging back down.
        public static void Dump(string header, List<Handle> handles)
        {
            MelonLogger.Msg($"[FruitLab] vitals — {header} ({handles.Count} parameter(s))");
            foreach (var h in handles)
            {
                if (!h.Valid) continue;
                try
                {
                    MelonLogger.Msg(
                        $"[FruitLab]   {h.Label}: value={Native.Value(h.Param):0.###} " +
                        $"max={Native.Max(h.Param):0.###} " +
                        $"(min={Native.Min(h.Param):0.###} ctorSeed={Native.Initial(h.Param):0.###})");
                }
                catch (Exception e) { MelonLogger.Msg($"[FruitLab]   {h.Label}: unreadable ({e.Message})"); }
            }
        }
    }

    /// <summary>
    /// Refuses writes to a frozen parameter. <c>bjq.jdl</c> is
    /// <c>LimitedValue.SetValue</c> — proven at runtime, not just by name matching:
    /// writing through it visibly collapsed a creature when FruitLab briefly targeted
    /// the wrong value.
    ///
    /// Blocking it is what makes a Lazarus dose an override rather than a tug of war.
    /// The solver still runs and still decides the creature is dead; it just cannot
    /// write that conclusion anywhere.
    /// </summary>
    [HarmonyPatch(typeof(bjq), nameof(bjq.jdl))]
    internal static class PatchLimitedValueSetValue
    {
        static bool Prefix(bjq __instance)
        {
            if (!Vitals.AnyFrozen) return true;
            try { return !Vitals.BlocksWrite(__instance); }
            catch { return true; }
        }
    }
}
