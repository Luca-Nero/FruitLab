using Il2Cpp;
using Il2CppEffectors;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Lazarus Syringe — a self-contained FruitLab item.
    //
    // Does what it says on the tin: keeps the body alive, and nothing else. It never
    // touches voxels, so wounds stay open and limbs stay off — it just holds the
    // creature's LVA vitals at their spawn values for as long as its charge lasts.
    //
    // The Healing Syringe restores voxels but not blood, so a body healed of every
    // wound still eventually runs out of blood to run on. This is the other half.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class LazarusSyringe
    {
        public const string DisplayName = "Lazarus Syringe";

        public  static readonly Color IconColor  = new Color(1f, 0.78f, 0.29f, 1f);   // amber
        private static readonly Color SpentColor = new Color(0.36f, 0.31f, 0.22f, 1f);
        private static readonly Vector3 PropScale = new Vector3(0.015f, 0.015f, 0.12f);

        private static readonly List<Dose> _live = new List<Dose>();

        private sealed class Dose
        {
            public GameObject Obj;
            public Rigidbody  Rb;

            // Stick
            public bool                 Stuck;
            public Rigidbody            HostRb;
            public LimbEffectorReceiver HostLer;
            public Vector3              LocalOffset;
            public Quaternion           LocalRotation;
            public Vector3              LastPos;

            // Charge
            public bool  Spent;
            public float SpentFor;
            public float Remaining;          // seconds of sustain left
            public bool  Faulted;

            // Sustain
            public Transform            CreatureRoot;
            public List<Vitals.Handle>  Handles = new List<Vitals.Handle>();
            public float                Accum;
            public bool                 Dumped;
            public bool                 Frozen;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loops
        // ══════════════════════════════════════════════════════════════════════════

        public static void OnSceneReload()
        {
            _live.Clear();
            Vitals.UnfreezeAll();
        }

        public static void OnUpdate()
        {
            if (_live.Count == 0) return;
            float dt = Time.deltaTime;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var d = _live[i];
                if (d.Obj == null) { Release(d); _live.RemoveAt(i); continue; }

                if (d.Spent)
                {
                    d.SpentFor += dt;
                    if (Config.SpentLifetime > 0f && d.SpentFor >= Config.SpentLifetime)
                    {
                        UnityEngine.Object.Destroy(d.Obj);
                        _live.RemoveAt(i);
                    }
                    continue;
                }

                if (!d.Stuck) continue;

                if (d.HostRb == null)
                {
                    Release(d);
                    UnityEngine.Object.Destroy(d.Obj);
                    _live.RemoveAt(i);
                    continue;
                }

                try
                {
                    var host = d.HostRb.transform;
                    d.Obj.transform.position = host.TransformPoint(d.LocalOffset);
                    d.Obj.transform.rotation = host.rotation * d.LocalRotation;

                    TickSustain(d, dt);
                }
                catch (Exception e) { Fault(d, e); }
            }
        }

        public static void OnFixedUpdate()
        {
            if (_live.Count == 0) return;
            float dt = Time.fixedDeltaTime;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var d = _live[i];
                if (d.Obj == null) { Release(d); _live.RemoveAt(i); continue; }
                if (d.Spent || d.Stuck) continue;

                try { TryStick(d, dt); }
                catch (Exception e) { Fault(d, e); }
            }
        }

        /// Hands this dose's parameters back to the solver. Safe to call twice; every
        /// path that drops a dose goes through it, because a hold left behind would
        /// pin a creature's vitals for the rest of the session.
        private static void Release(Dose d)
        {
            if (!d.Frozen) return;
            d.Frozen = false;
            try { Vitals.Unfreeze(d.Handles); } catch { }
        }

        private static void Fault(Dose d, Exception e)
        {
            if (!d.Faulted)
            {
                d.Faulted = true;
                MelonLogger.Warning($"[FruitLab] Lazarus dose retired after an error: {e.Message}");
            }

            Release(d);

            d.Stuck = false;
            d.Spent = true;
            try { if (d.Rb != null) { d.Rb.isKinematic = false; d.Rb.WakeUp(); } } catch { }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Throw / stick / spend
        // ══════════════════════════════════════════════════════════════════════════

        public static void Throw()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var obj = Props.Spawn("FruitLab_Lazarus", PropScale, IconColor);
            if (obj == null) return;

            Vector3 muzzle = cam.transform.position;
            Vector3 fwd    = cam.transform.forward;

            obj.transform.position = muzzle + fwd * 0.5f;
            obj.transform.rotation = cam.transform.rotation;

            var d = new Dose { Obj = obj, Remaining = Config.LazarusDuration };
            d.Rb                = obj.AddComponent<Rigidbody>();
            d.Rb.mass           = 0.1f;
            d.Rb.linearVelocity = fwd * Config.ThrowSpeed;
            d.LastPos           = muzzle;

            _live.Add(d);
        }

        public static void RecallAll()
        {
            if (_live.Count == 0) return;

            int n = _live.Count;
            foreach (var d in _live)
            {
                Release(d);
                d.Stuck = false;
                if (d.Obj != null) UnityEngine.Object.Destroy(d.Obj);
            }
            _live.Clear();
            MelonLogger.Msg($"[FruitLab] Recalled {n} Lazarus dose(s).");
        }

        private static void TryStick(Dose d, float dt)
        {
            var rb = d.Rb;
            if (rb == null || rb.isKinematic || rb.IsSleeping()) return;

            float radius = Mathf.Max(Config.StickRadius, 0.005f);

            Vector3 from = d.LastPos;
            Vector3 to   = rb.position + rb.linearVelocity * dt;
            d.LastPos    = rb.position;

            if (Props.SweepForLimb(d.Obj, from, to, radius, out Collider limb, out Vector3 point)
                != SweepResult.Hit) return;

            Vector3 seg = to - from;
            Vector3 dir = seg.sqrMagnitude > 1e-8f ? seg.normalized : d.Obj.transform.forward;

            var ler    = Limbs.Of(limb.gameObject);
            var hostRb = limb.attachedRigidbody;
            if (ler == null || hostRb == null) return;

            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;

            d.Obj.transform.position = point;
            d.Obj.transform.rotation = Quaternion.LookRotation(dir);

            d.Stuck         = true;
            d.HostRb        = hostRb;
            d.HostLer       = ler;
            d.LocalOffset   = hostRb.transform.InverseTransformPoint(point);
            d.LocalRotation = Quaternion.Inverse(hostRb.transform.rotation) * d.Obj.transform.rotation;

            var own = d.Obj.GetComponent<Collider>();
            if (own != null) own.enabled = false;

            BindVitals(d);
        }

        private static void Spend(Dose d)
        {
            Release(d);

            d.Stuck        = false;
            d.Spent        = true;
            d.SpentFor     = 0f;
            d.HostRb       = null;
            d.HostLer      = null;
            d.CreatureRoot = null;
            d.Handles.Clear();

            if (d.Obj != null)
            {
                // Back the needle out before the collider comes back, or depenetration
                // flings it — half its length is inside the limb it entered.
                d.Obj.transform.position -= d.Obj.transform.forward * 0.07f;

                var col = d.Obj.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                Props.Tint(d.Obj, SpentColor);
                d.LastPos = d.Obj.transform.position;
            }

            if (d.Rb != null)
            {
                d.Rb.isKinematic     = false;
                d.Rb.linearVelocity  = Vector3.zero;
                d.Rb.angularVelocity = Vector3.zero;
                d.Rb.WakeUp();
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Sustain
        // ══════════════════════════════════════════════════════════════════════════

        /// Collects every parameter the dose will hold up: the creature's own, plus
        /// each limb's. Done once on impact — the set does not change while the dose
        /// runs, and walking the creature every tick would be wasteful.
        private static void BindVitals(Dose d)
        {
            d.Handles.Clear();
            d.Accum = 0f;

            if (d.HostLer == null) return;

            Transform root = Limbs.CreatureRootOf(d.HostLer);
            if (root == null) return;
            d.CreatureRoot = root;

            var creature = Limbs.CreatureOf(d.HostLer);
            if (creature != null) Vitals.CollectCreature(creature, d.Handles);

            foreach (var ler in root.GetComponentsInChildren<LimbEffectorReceiver>(true))
                if (ler != null) Vitals.CollectLimb(ler, d.Handles);

            if (d.Handles.Count == 0)
            {
                MelonLogger.Warning(
                    $"[FruitLab] Lazarus stuck to {root.name} but found no vitals to hold.");
                return;
            }

            if (Config.LogVitals) { Vitals.Dump("on impact", d.Handles); d.Dumped = true; }

            // Pin them, rather than re-asserting on a cadence. The solver keeps
            // computing "dead" from the destroyed organs; letting it write that
            // between our writes is what made the body convulse.
            Vitals.Freeze(d.Handles);
            d.Frozen = true;

            // A dose stands a dead body up, and a dead body has had its feet switched to
            // a frictionless material. Without this it stands there skating.
            Puppeteer.RestoreFootFriction(root);

            MelonLogger.Msg(
                $"[FruitLab] Lazarus holding {d.Handles.Count} vital(s) on {root.name} " +
                $"for {Config.LazarusDuration:0.#}s.");
        }

        private static void TickSustain(Dose d, float dt)
        {
            d.Remaining -= dt;
            if (d.Remaining <= 0f)
            {
                if (Config.LogVitals && d.Dumped) Vitals.Dump("dose expired", d.Handles);
                MelonLogger.Msg("[FruitLab] Lazarus dose expired.");
                Spend(d);
                return;
            }

            if (d.Handles.Count == 0) { Spend(d); return; }

            d.Accum += dt;
            float interval = Mathf.Max(Config.LazarusInterval, 0.02f);
            if (d.Accum < interval) return;
            d.Accum = 0f;

            // The dependency solver recomputes these from organ state, so a restore
            // does not stick — holding the body up means re-applying, which is what
            // "for the duration of the syringe" buys.
            Vitals.RestoreAll(d.Handles);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Organ teardown hold — read by PatchOrganDestroyLVA via Items
        // ══════════════════════════════════════════════════════════════════════════

        /// A Lazarus dose is the one thing in the mod that most needs organs kept
        /// alive: it is explicitly refusing to let the creature die.
        public static bool AnyPassRunning
        {
            get
            {
                foreach (var d in _live)
                    if (d.Stuck && !d.Spent && d.CreatureRoot != null) return true;
                return false;
            }
        }

        public static bool HoldsOrganTeardown(Transform organ)
        {
            if (organ == null) return false;

            foreach (var d in _live)
            {
                if (!d.Stuck || d.Spent || d.CreatureRoot == null) continue;
                if (organ.IsChildOf(d.CreatureRoot)) return true;
            }
            return false;
        }
    }
}
