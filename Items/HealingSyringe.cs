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
    // Healing Syringe — a self-contained FruitLab item.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class HealingSyringe
    {
        public const string DisplayName = "Healing Syringe";

        public  static readonly Color IconColor  = new Color(0.75f, 0.93f, 1f,   1f);
        private static readonly Color SpentColor = new Color(0.32f, 0.34f, 0.36f, 1f);
        private static readonly Vector3 PropScale = new Vector3(0.015f, 0.015f, 0.12f);

        private const float SettleWindow   = 0.9f;
        private const float SettleInterval = 0.15f;
        private const float SettleWriteFor = 0.5f;

        private const float RecheckInterval = 0.1f;

        private static readonly List<Syringe> _live = new List<Syringe>();

        // ── Item state ────────────────────────────────────────────────────────

        private sealed class Target
        {
            public LimbEffectorReceiver Ler;
            public zf                   Shape;
            public string               Label;

            public Vector3Int Origin;
            public int        Length, Height, Width;

            public float StartDelay;
            public float Radius;
            public float MaxRadius;
            public bool  Done;
        }

        private sealed class Syringe
        {
            public GameObject Obj;
            public Rigidbody  Rb;

            public bool                 Stuck;
            public Rigidbody            HostRb;
            public LimbEffectorReceiver HostLer;
            public Vector3              LocalOffset;
            public Quaternion           LocalRotation;
            public Vector3              LastPos;

            public bool  Disposable = true;
            public bool  Spent;
            public float SpentFor;
            public bool  Faulted;

            public Transform     CreatureRoot;
            public List<Target>  Targets    = new List<Target>();
            public List<Rigidbody> RagdollRbs = new List<Rigidbody>();
            public float Elapsed;
            public float TickAccum;

            public float PlungeSpan = 1f;
            public bool  Idle;
            public float IdleTimer;
            public int   DamageWatermark = -1;

            public bool  Restored;
            public float SettleFor;
            public float SettleAccum;
            public int   RestoredTotal;
            public bool  DumpedBefore;
            public bool  WarnedHeadless;
            public bool  WarnedEmpty;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loops
        // ══════════════════════════════════════════════════════════════════════════

        public static bool TreatingOn(Transform creatureRoot)
        {
            if (creatureRoot == null) return false;

            foreach (var s in _live)
                if (s.Stuck && !s.Spent && s.CreatureRoot == creatureRoot) return true;

            return false;
        }

        public static void OnSceneReload() => _live.Clear();

        public static void OnUpdate()
        {
            if (_live.Count == 0) return;
            float dt = Time.deltaTime;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                if (s.Obj == null) { _live.RemoveAt(i); continue; }

                if (s.Spent)
                {
                    s.SpentFor += dt;
                    if (Config.SpentLifetime > 0f && s.SpentFor >= Config.SpentLifetime)
                    {
                        UnityEngine.Object.Destroy(s.Obj);
                        _live.RemoveAt(i);
                    }
                    continue;
                }

                if (!s.Stuck) continue;

                if (s.HostRb == null)
                {
                    UnityEngine.Object.Destroy(s.Obj);
                    _live.RemoveAt(i);
                    continue;
                }

                try
                {
                    var host = s.HostRb.transform;
                    s.Obj.transform.position = host.TransformPoint(s.LocalOffset);
                    s.Obj.transform.rotation = host.rotation * s.LocalRotation;

                    TickPass(s, dt);
                }
                catch (Exception e) { Fault(s, e); }
            }
        }

        public static void OnFixedUpdate()
        {
            if (_live.Count == 0) return;
            float dt = Time.fixedDeltaTime;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                if (s.Obj == null) { _live.RemoveAt(i); continue; }
                if (s.Spent) continue;

                try
                {
                    if (s.Stuck)
                    {
                        if (s.Idle) continue;
                        foreach (var rb in s.RagdollRbs)
                            if (rb != null && rb.IsSleeping()) rb.WakeUp();
                    }
                    else TryStick(s, dt);
                }
                catch (Exception e) { Fault(s, e); }
            }
        }

        private static void Fault(Syringe s, Exception e)
        {
            if (!s.Faulted)
            {
                s.Faulted = true;
                MelonLogger.Warning($"[FruitLab] Syringe retired after an error: {e.Message}");
            }

            s.Stuck = false;
            s.Spent = true;
            try { if (s.Rb != null) { s.Rb.isKinematic = false; s.Rb.WakeUp(); } } catch { }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Throw / stick / spend
        // ══════════════════════════════════════════════════════════════════════════

        public static void Throw()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var obj = SyringeModel.Spawn("FruitLab_Syringe", PropScale, IconColor);
            if (obj == null) return;

            Vector3 muzzle = cam.transform.position;
            Vector3 fwd    = cam.transform.forward;

            obj.transform.position = muzzle + fwd * 0.5f;
            obj.transform.rotation = cam.transform.rotation;

            var s = new Syringe { Obj = obj };
            s.Rb                = obj.AddComponent<Rigidbody>();
            s.Rb.mass           = 0.1f;
            s.Rb.linearVelocity = fwd * Config.ThrowSpeed;

            s.LastPos = muzzle;

            _live.Add(s);
        }

        public static void RecallAll()
        {
            if (_live.Count == 0) return;

            int n = _live.Count;
            foreach (var s in _live)
            {
                s.Stuck = false;
                if (s.Obj != null) UnityEngine.Object.Destroy(s.Obj);
            }
            _live.Clear();
            MelonLogger.Msg($"[FruitLab] Recalled {n} syringe(s).");
        }

        private static void TryStick(Syringe s, float dt)
        {
            var rb = s.Rb;
            if (rb == null || rb.isKinematic) return;
            if (rb.IsSleeping()) return;

            float radius = Mathf.Max(Config.StickRadius, 0.005f);

            Vector3 from = s.LastPos;
            Vector3 to   = rb.position + rb.linearVelocity * dt;
            s.LastPos    = rb.position;

            if (Props.SweepForLimb(s.Obj, from, to, radius, out Collider limb, out Vector3 point)
                != SweepResult.Hit) return;

            Vector3 seg = to - from;
            Vector3 dir = seg.sqrMagnitude > 1e-8f ? seg.normalized : s.Obj.transform.forward;

            StickTo(s, limb, point, dir);
        }

        private static void StickTo(Syringe s, Collider col, Vector3 point, Vector3 dir)
        {
            var ler    = Limbs.Of(col.gameObject);
            var hostRb = col.attachedRigidbody;
            if (ler == null || hostRb == null) return;

            s.Rb.linearVelocity  = Vector3.zero;
            s.Rb.angularVelocity = Vector3.zero;
            s.Rb.isKinematic     = true;

            s.Obj.transform.position = point;
            s.Obj.transform.rotation = Quaternion.LookRotation(dir);

            s.Stuck         = true;
            s.HostRb        = hostRb;
            s.HostLer       = ler;
            s.LocalOffset   = hostRb.transform.InverseTransformPoint(point);
            s.LocalRotation = Quaternion.Inverse(hostRb.transform.rotation) * s.Obj.transform.rotation;

            var own = s.Obj.GetComponent<Collider>();
            if (own != null) own.enabled = false;

            StartPass(s);
        }

        private static void Spend(Syringe s)
        {
            s.Stuck        = false;
            s.Spent        = true;
            s.SpentFor     = 0f;
            s.Idle         = false;
            s.HostRb       = null;
            s.HostLer      = null;
            s.CreatureRoot = null;
            s.Targets.Clear();
            s.RagdollRbs.Clear();

            if (s.Obj != null)
            {
                s.Obj.transform.position -= s.Obj.transform.forward * 0.07f;

                var col = s.Obj.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                SyringeModel.Tint(s.Obj, SpentColor);

                SyringeModel.Plunge(s.Obj, 1f);
                s.LastPos = s.Obj.transform.position;
            }

            if (s.Rb != null)
            {
                s.Rb.isKinematic     = false;
                s.Rb.linearVelocity  = Vector3.zero;
                s.Rb.angularVelocity = Vector3.zero;
                s.Rb.WakeUp();
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Heal wave
        // ══════════════════════════════════════════════════════════════════════════

        private static void StartPass(Syringe s)
        {
            s.Targets.Clear();
            s.RagdollRbs.Clear();
            s.Elapsed     = 0f;
            s.TickAccum   = 0f;
            s.Idle        = false;
            s.IdleTimer   = 0f;
            s.Restored    = false;
            s.SettleFor   = 0f;
            s.SettleAccum = 0f;

            if (s.Obj == null || s.HostLer == null) return;

            Transform root = Limbs.CreatureRootOf(s.HostLer);
            if (root == null) return;
            s.CreatureRoot = root;

            Vector3 impact  = s.Obj.transform.position;
            var     pending = new List<KeyValuePair<float, Target>>();

            foreach (var ler in root.GetComponentsInChildren<LimbEffectorReceiver>(true))
            {
                if (ler == null) continue;

                var mesh = Limbs.MeshOf(ler);
                if (mesh == null) continue;
                if (!Limbs.TryGetGrid(mesh, out int len, out int hgt, out int wid)) continue;
                if (!Limbs.TryVoxelIndex(mesh, impact, len, hgt, wid, out Vector3Int origin)) continue;

                var rb = ler.GetComponentInParent<Rigidbody>();
                if (rb != null && !s.RagdollRbs.Contains(rb)) s.RagdollRbs.Add(rb);

                float dist = Vector3.Distance(Limbs.SamplePointOf(ler), impact);

                pending.Add(new KeyValuePair<float, Target>(dist, new Target
                {
                    Ler        = ler,
                    Shape      = Limbs.ShapeOf(ler),
                    Label      = ler.transform.parent != null ? ler.transform.parent.name : ler.name,
                    Origin     = origin,
                    Length     = len,
                    Height     = hgt,
                    Width      = wid,
                    StartDelay = Config.HealWorldSpeed > 0f ? dist / Config.HealWorldSpeed : 0f,
                    Radius     = 0f,
                    MaxRadius  = CornerRadius(origin, len, hgt, wid),
                    Done       = false,
                }));
            }

            pending.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var p in pending) s.Targets.Add(p.Value);

            float interval = Mathf.Max(Config.HealTickInterval, 0.001f);
            float speed    = Mathf.Max(Config.HealWaveSpeed, 0.01f);
            float span     = 0f;

            foreach (var t in s.Targets)
                span = Mathf.Max(span, t.StartDelay + t.MaxRadius / speed * interval);

            s.PlungeSpan = Mathf.Max(span + SettleWindow, 0.05f);

            if (s.Targets.Count > 0)
                MelonLogger.Msg($"[FruitLab] Healing {s.Targets.Count} limb(s) " +
                                $"on {Patients.NameFor(root)}.");
            else if (!s.WarnedEmpty)
            {
                s.WarnedEmpty = true;
                MelonLogger.Warning($"[FruitLab] Stuck to {Patients.NameFor(root)} " +
                                    "but found no healable limbs.");
            }
        }

        private static void TickPass(Syringe s, float dt)
        {
            if (s.Idle)
            {
                s.IdleTimer += dt;
                float wait = s.Targets.Count > 0 ? RecheckInterval : Mathf.Max(RecheckInterval, 1f);
                if (s.IdleTimer < wait) return;
                s.IdleTimer = 0f;

                int now = DisabledVoxelTotal(s);
                if (now < 0 || now != s.DamageWatermark) StartPass(s);
                return;
            }

            if (s.Targets.Count == 0) { FinishPass(s); return; }

            s.TickAccum += dt;
            float interval = Mathf.Max(Config.HealTickInterval, 0.001f);

            for (int guard = 0; guard < 4 && s.TickAccum >= interval; guard++)
            {
                s.TickAccum -= interval;
                s.Elapsed   += interval;
                StepWave(s);
            }
            if (s.TickAccum > interval) s.TickAccum = interval;

            SyringeModel.Plunge(s.Obj, (s.Elapsed + s.SettleFor) / s.PlungeSpan);

            foreach (var t in s.Targets) if (!t.Done) return;

            s.SettleFor   += dt;
            s.SettleAccum += dt;

            if (s.SettleFor < SettleWriteFor && (!s.Restored || s.SettleAccum >= SettleInterval))
            {
                s.SettleAccum   = 0f;
                s.Restored      = true;
                s.RestoredTotal += RestoreVitals(s);
            }

            if (s.SettleFor >= SettleWindow) FinishPass(s);
        }

        private static void FinishPass(Syringe s)
        {
            ReportRestore(s);

            if (s.Disposable)
            {
                MelonLogger.Msg("[FruitLab] Dose spent.");
                Spend(s);
                return;
            }

            s.Idle            = true;
            s.IdleTimer       = 0f;
            s.DamageWatermark = DisabledVoxelTotal(s);
        }

        private static int RestoreVitals(Syringe s)
        {
            if (s.HostLer == null || s.CreatureRoot == null) return 0;

            var inputs  = new List<Vitals.Handle>();
            var outputs = new List<Vitals.Handle>();
            Gather(s, inputs, outputs);
            if (inputs.Count == 0 && outputs.Count == 0) return 0;

            if (Config.LogVitals && !s.DumpedBefore)
            {
                s.DumpedBefore = true;
                Vitals.Dump("before restore — inputs", inputs);
                Vitals.Dump("before restore — outputs", outputs);
            }

            if (!Limbs.HasHead(s.CreatureRoot))
            {
                if (!s.WarnedHeadless)
                {
                    s.WarnedHeadless = true;
                    MelonLogger.Msg("[FruitLab] No head on this one — mending it, but not waking it.");
                }
                return 0;
            }

            Puppeteer.RestoreFootFriction(s.CreatureRoot);

            return Vitals.RestoreAll(inputs,  skipPosture: true)
                 + Vitals.RestoreAll(outputs, skipPosture: true);
        }

        private static void Gather(Syringe s, List<Vitals.Handle> inputs,
                                              List<Vitals.Handle> outputs)
        {
            var creature = Limbs.CreatureOf(s.HostLer);
            if (creature != null)
            {
                Vitals.CollectExternal(creature, "creature", inputs);
                Vitals.CollectCreature(creature, outputs);
            }

            foreach (var ler in s.CreatureRoot.GetComponentsInChildren<LimbEffectorReceiver>(true))
            {
                if (ler == null) continue;
                Vitals.CollectLimbExternal(ler, inputs);
                Vitals.CollectLimb(ler, outputs);
            }

            Vitals.CollectOrgans(s.CreatureRoot, outputs, inputs);
        }

        private static void ReportRestore(Syringe s)
        {
            if (s.RestoredTotal == 0 || s.CreatureRoot == null) return;

            MelonLogger.Msg(
                $"[FruitLab] Restored {s.RestoredTotal} vital(s) " +
                $"on {Patients.NameFor(s.CreatureRoot)}.");

            if (!Config.LogVitals) return;

            var inputs  = new List<Vitals.Handle>();
            var outputs = new List<Vitals.Handle>();
            Gather(s, inputs, outputs);
            Vitals.Dump("settled — inputs", inputs);
            Vitals.Dump("settled — outputs", outputs);
        }

        private static void StepWave(Syringe s)
        {
            foreach (var t in s.Targets)
            {
                if (t.Done || s.Elapsed < t.StartDelay) continue;

                float prev = t.Radius;
                t.Radius += Mathf.Max(Config.HealWaveSpeed, 0.01f);

                EmitShell(t, prev, t.Radius);

                if (t.Radius >= t.MaxRadius) t.Done = true;
            }
        }

        private static void EmitShell(Target t, float inner, float outer)
        {
            float innerSq = inner * inner;
            float outerSq = outer * outer;
            bool  fromZero = inner <= 0f;

            int count = CountShell(t, innerSq, outerSq, fromZero);
            if (count == 0) return;

            var batch = Limbs.NewBatch(count);
            try
            {
                for (int z = 0; z < t.Width; z++)
                    for (int y = 0; y < t.Height; y++)
                    {
                        int dy = y - t.Origin.y, dz = z - t.Origin.z;
                        int yz = dy * dy + dz * dz;
                        if (yz > outerSq) continue;
                        for (int x = 0; x < t.Length; x++)
                        {
                            int dx = x - t.Origin.x;
                            int d2 = dx * dx + yz;
                            if (d2 <= outerSq && (d2 > innerSq || fromZero))
                                Limbs.Add(batch, x, y, z, Config.HealSignal,
                                          InfluenceProcessType.Equate);
                        }
                    }

                Limbs.Send(t.Ler, batch, t.Label);
            }
            finally { batch.Dispose(); }
        }

        private static int CountShell(Target t, float innerSq, float outerSq, bool fromZero)
        {
            int count = 0;
            for (int z = 0; z < t.Width; z++)
                for (int y = 0; y < t.Height; y++)
                {
                    int dy = y - t.Origin.y, dz = z - t.Origin.z;
                    int yz = dy * dy + dz * dz;
                    if (yz > outerSq) continue;
                    for (int x = 0; x < t.Length; x++)
                    {
                        int dx = x - t.Origin.x;
                        int d2 = dx * dx + yz;
                        if (d2 <= outerSq && (d2 > innerSq || fromZero)) count++;
                    }
                }
            return count;
        }

        private static float CornerRadius(Vector3Int o, int len, int hgt, int wid)
        {
            int dx = Math.Max(o.x, len - 1 - o.x);
            int dy = Math.Max(o.y, hgt - 1 - o.y);
            int dz = Math.Max(o.z, wid - 1 - o.z);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static int DisabledVoxelTotal(Syringe s)
        {
            int  total = 0;
            bool any    = false;

            foreach (var t in s.Targets)
            {
                int n = Limbs.DisabledVoxels(t.Shape);
                if (n < 0) continue;
                total += n;
                any    = true;
            }

            return any ? total : -1;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Organ teardown hold — read by PatchOrganDestroyLVA via Items
        // ══════════════════════════════════════════════════════════════════════════

        public static bool AnyPassRunning
        {
            get
            {
                foreach (var s in _live)
                    if (s.Stuck && !s.Idle && s.CreatureRoot != null) return true;
                return false;
            }
        }

        public static bool HoldsOrganTeardown(Transform organ)
        {
            if (organ == null) return false;

            foreach (var s in _live)
            {
                if (!s.Stuck || s.Idle || s.CreatureRoot == null) continue;
                if (organ.IsChildOf(s.CreatureRoot)) return true;
            }
            return false;
        }
    }
}
