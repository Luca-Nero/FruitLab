using Il2Cpp;
using Il2CppEffectors;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Lazarus Syringe — a self-contained FruitLab item.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class LazarusSyringe
    {
        public const string DisplayName = "Lazarus Syringe";

        public  static readonly Color IconColor  = new Color(1f, 0.78f, 0.29f, 1f);
        private static readonly Color SpentColor = new Color(0.36f, 0.31f, 0.22f, 1f);
        private static readonly Vector3 PropScale = new Vector3(0.015f, 0.015f, 0.12f);

        private static readonly List<Dose> _live = new List<Dose>();

        private sealed class Dose
        {
            public GameObject Obj;
            public Rigidbody  Rb;

            public bool                 Stuck;
            public Rigidbody            HostRb;
            public LimbEffectorReceiver HostLer;
            public Vector3              LocalOffset;
            public Quaternion           LocalRotation;
            public Vector3              LastPos;

            public bool  Spent;
            public float SpentFor;
            public float Remaining;
            public bool  Faulted;

            public Transform            CreatureRoot;
            public List<Vitals.Handle>  Handles = new List<Vitals.Handle>();
            public float                Accum;
            public bool                 Dumped;
            public bool                 Frozen;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loops
        // ══════════════════════════════════════════════════════════════════════════

        public static float SustainOn(Transform creatureRoot)
        {
            if (creatureRoot == null) return 0f;

            float most = 0f;

            foreach (var d in _live)
            {
                if (!d.Frozen || d.CreatureRoot != creatureRoot) continue;
                if (d.Remaining > most) most = d.Remaining;
            }

            return most;
        }

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

        private const float WakeSignal = 1f;

        private static void Release(Dose d)
        {
            if (!d.Frozen) return;
            d.Frozen = false;
            try { Vitals.Unfreeze(d.Handles); } catch { }

            Wake(d);
        }

        private static void Wake(Dose d)
        {
            if (d.HostLer == null) return;

            try
            {
                var shape = Limbs.ShapeOf(d.HostLer);
                var mesh  = Limbs.MeshOf(d.HostLer);
                if (mesh == null || shape == null) return;
                if (!Limbs.TryGetGrid(mesh, out int len, out int hgt, out int wid)) return;

                var batch = Limbs.NewBatch(1);
                try
                {
                    Limbs.Add(batch, len / 2, hgt / 2, wid / 2, -WakeSignal,
                              InfluenceProcessType.Sum);
                    Limbs.Send(d.HostLer, batch, "lazarus release");
                }
                finally { batch.Dispose(); }
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Could not wake the patient: {e.Message}");
            }
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

            var obj = SyringeModel.Spawn("FruitLab_Lazarus", PropScale, IconColor);
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
                d.Obj.transform.position -= d.Obj.transform.forward * 0.07f;

                var col = d.Obj.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                SyringeModel.Tint(d.Obj, SpentColor);
                SyringeModel.Plunge(d.Obj, 1f);
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
                    $"[FruitLab] Lazarus stuck to {Patients.NameFor(root)} " +
                    "but found no vitals to hold.");
                return;
            }

            if (Config.LogVitals) { Vitals.Dump("on impact", d.Handles); d.Dumped = true; }

            Vitals.Freeze(d.Handles);
            d.Frozen = true;

            Puppeteer.RestoreFootFriction(root);

            MelonLogger.Msg(
                $"[FruitLab] Lazarus holding {d.Handles.Count} vital(s) " +
                $"on {Patients.NameFor(root)} " +
                $"for {Config.LazarusDuration:0.#}s.");
        }

        private static void TickSustain(Dose d, float dt)
        {
            d.Remaining -= dt;

            float span = Mathf.Max(Config.LazarusDuration, 0.05f);
            SyringeModel.Plunge(d.Obj, 1f - d.Remaining / span);

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

            Vitals.RestoreAll(d.Handles);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Organ teardown hold — read by PatchOrganDestroyLVA via Items
        // ══════════════════════════════════════════════════════════════════════════

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
