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
    internal static class Vitals
    {
        internal readonly struct Handle
        {
            public readonly bjq    Param;
            public readonly string Key;
            public readonly string Owner;

            public Handle(bjq param, string key, string owner)
            {
                Param = param; Key = key; Owner = owner;
            }

            public string Label => Owner + "/" + Key;

            public bool  Valid    => Param != null;
            public float Value    { get { try { return Native.Value(Param); } catch { return 0f; } } }
            public float Max      { get { try { return Native.Max(Param);   } catch { return 0f; } } }
            public float Fraction { get { float m = Max; return m > 0.0001f ? Mathf.Clamp01(Value / m) : 0f; } }
            public bool  Frozen   => IsFrozen(Param);
        }

        // ── Collection ────────────────────────────────────────────────────────

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

                    string name;
                    try { name = kv.Key != null ? kv.Key.Name : "?"; } catch { name = "?"; }

                    into.Add(new Handle(param, name, prefix));
                    found++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FruitLab] vitals walk failed on {prefix}: {ex.Message}");
            }

            if (found == 0)
            {
                int size = -1;
                try { size = map.Count; } catch { }
                MelonLogger.Warning($"[FruitLab] no vitals on {prefix} (module reports {size} entries)");
            }

            return found;
        }

        public static int CollectCreature(bcx creature, List<Handle> into) =>
            Collect(creature, "creature", into);

        public static int CollectLimb(LimbEffectorReceiver ler, List<Handle> into)
        {
            var entity = LimbEntityOf(ler, out string label);
            return entity == null ? 0 : Collect(entity, label, into);
        }

        public static int CollectExternal(bcx entity, string prefix, List<Handle> into)
        {
            if (entity == null) return 0;

            Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Type, bcx.bct> map;
            try
            {
                var module = Native.ExternalModule(entity);
                if (module == null) return 0;
                map = Native.ExternalMap(module);
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
                    if (kv.Value == null) continue;

                    bda param;
                    try { param = Native.ExternalParam(kv.Value); } catch { continue; }
                    if (param == null) continue;

                    string name;
                    try { name = kv.Key != null ? kv.Key.Name : "?"; } catch { name = "?"; }

                    into.Add(new Handle(param, name, prefix));
                    found++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FruitLab] external vitals walk failed on {prefix}: {ex.Message}");
            }

            return found;
        }

        public static int CollectLimbExternal(LimbEffectorReceiver ler, List<Handle> into)
        {
            var entity = LimbEntityOf(ler, out string label);
            return entity == null ? 0 : CollectExternal(entity, label, into);
        }

        public static int CollectOrgans(Transform creatureRoot, List<Handle> into) =>
            CollectOrgans(creatureRoot, into, null);

        public static int CollectOrgans(Transform creatureRoot, List<Handle> into,
                                        List<Handle> externals)
        {
            if (creatureRoot == null) return 0;

            int found = 0;
            foreach (var organ in creatureRoot.GetComponentsInChildren<rx>(true))
            {
                if (organ == null) continue;
                string label;
                try { label = organ.gameObject.name; } catch { label = "organ"; }

                if (into != null)     found += Collect(organ, label, into);
                if (externals != null) CollectExternal(organ, label, externals);
            }
            return found;
        }

        private static bcx LimbEntityOf(LimbEffectorReceiver ler, out string label)
        {
            label = "limb";
            try
            {
                var refs = ler.m_limbReferences;
                if (refs == null) return null;
                label = ler.transform.parent != null ? ler.transform.parent.name : ler.name;
                return Native.LimbEntity(refs);
            }
            catch { return null; }
        }

        // ── Restore ───────────────────────────────────────────────────────────

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

        private static readonly string[] Posture =
            { "rh", "ri", "xq", "bbk", "bbh", "xr", "zr" };

        public static bool IsPosture(string key)
        {
            for (int i = 0; i < Posture.Length; i++) if (Posture[i] == key) return true;
            return false;
        }

        public static int RestoreAll(List<Handle> handles, bool skipPosture = false)
        {
            _selfWriting = true;
            try
            {
                int written = 0;
                foreach (var h in handles)
                {
                    if (skipPosture && IsPosture(h.Key)) continue;
                    if (Restore(h)) written++;
                }
                return written;
            }
            finally { _selfWriting = false; }
        }

        // ── Freeze ────────────────────────────────────────────────────────────

        private static readonly Dictionary<IntPtr, int> _frozen = new Dictionary<IntPtr, int>();
        private static bool _selfWriting;

        public static bool AnyFrozen => _frozen.Count > 0;

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

        public static void UnfreezeAll() => _frozen.Clear();

        internal static bool IsFrozen(bjq value)
        {
            if (_frozen.Count == 0 || value == null) return false;
            try { return _frozen.ContainsKey(IL2CPP.Il2CppObjectBaseToPtr(value)); }
            catch { return false; }
        }

        internal static bool BlocksWrite(bjq value)
        {
            if (_frozen.Count == 0 || _selfWriting || value == null) return false;
            try { return _frozen.ContainsKey(IL2CPP.Il2CppObjectBaseToPtr(value)); }
            catch { return false; }
        }

        // ── Diagnostics ───────────────────────────────────────────────────────

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
