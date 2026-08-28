using FruitLib;
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
    //
    // Owns its toolbar slot, its thrown props, and its heal wave. Everything shared
    // with future items lives in Limbs.cs (body plumbing), Props.cs (physical props
    // and flight paths) and Native.cs (obfuscated name mapping); nothing outside
    // this file knows what a syringe is.
    //
    // Core drives it through Register / OnUpdate / OnFixedUpdate / OnSceneReload.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class HealingSyringe
    {
        public const string ItemId = "FruitLab:HealingSyringe";

        private static readonly Color FullColor  = new Color(0.75f, 0.93f, 1f,   1f);
        private static readonly Color SpentColor = new Color(0.32f, 0.34f, 0.36f, 1f);
        private static readonly Vector3 PropScale = new Vector3(0.015f, 0.015f, 0.12f);

        /// Seconds a non-disposable syringe waits between checks for fresh damage.
        /// Not a config knob: every syringe is disposable today, so it would show up
        /// in the menu doing nothing. Promote it back when a refillable variant lands.
        private const float RecheckInterval = 0.1f;

        private static readonly List<Syringe> _live = new List<Syringe>();
        private static bool _equipped;

        // ── Item state ────────────────────────────────────────────────────────

        /// One limb inside one syringe's heal pass.
        private sealed class Target
        {
            public LimbEffectorReceiver Ler;
            public zf                   Shape;   // null if the limb exposes no shape data
            public string               Label;

            public Vector3Int Origin;             // impact point, clamped into this grid
            public int        Length, Height, Width;

            public float StartDelay;              // before the wave reaches this limb
            public float Radius;                  // wave front, voxel units
            public float MaxRadius;               // furthest corner of the grid
            public bool  Done;
        }

        private sealed class Syringe
        {
            public GameObject Obj;
            public Rigidbody  Rb;

            // Stick
            public bool                 Stuck;
            public Rigidbody            HostRb;
            public LimbEffectorReceiver HostLer;
            public Vector3              LocalOffset;
            public Quaternion           LocalRotation;
            public Vector3              LastPos;      // for the swept stick test

            // Dose
            /// A disposable syringe carries one dose: it runs a single heal wave, then
            /// detaches and drops. Set at throw time, so a refillable variant only has
            /// to clear it to get the keep-healing-forever behaviour back.
            public bool  Disposable = true;
            public bool  Spent;
            public float SpentFor;
            public bool  Faulted;                  // a game API threw; retired, logged once

            // Healing
            public Transform     CreatureRoot;      // whose limbs this pass covers
            public List<Target>  Targets    = new List<Target>();
            public List<Rigidbody> RagdollRbs = new List<Rigidbody>();
            public float Elapsed;                  // seconds into the current pass
            public float TickAccum;
            public bool  Idle;                     // pass done, watching for new damage
            public float IdleTimer;
            public int   DamageWatermark = -1;
            public bool  WarnedEmpty;              // "no healable limbs" logs once
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Toolbar item
        // ══════════════════════════════════════════════════════════════════════════

        public static void Register()
        {
            FruitToolbar.Register(new FruitToolbarItem
            {
                Id           = ItemId,
                Name         = "Healing Syringe",
                Icon         = FruitToolbar.MakeSolidIcon(FullColor),
                OnSelected   = OnSelected,
                OnDeselected = OnDeselected,
            });
        }

        private static void OnSelected(int slot)
        {
            _equipped = true;
            MelonLogger.Msg($"[FruitLab] Healing Syringe equipped (slot {slot + 1}).");
        }

        private static void OnDeselected(int slot) => _equipped = false;

        /// FruitToolbar drops its selection on a scene change without dispatching the
        /// deselect callback, so the equipped flag has to be cleared here or the
        /// syringe stays armed on left click into the next scene. Scene load has also
        /// already destroyed every prop and limb we were holding.
        public static void OnSceneReload()
        {
            _equipped = false;
            _live.Clear();
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loops
        // ══════════════════════════════════════════════════════════════════════════

        public static void OnUpdate()
        {
            // Clicks and keys aimed at FruitLib's menu are not gameplay input.
            if (!FruitMenu.BlocksGameplayInput)
            {
                if (_equipped && Input.GetMouseButtonDown(0)) Throw();
                if (Input.GetKeyDown(Config.RecallKey))        RecallAll();
            }

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
                        // Hold the ragdoll awake only while a wave is actually running,
                        // or the voxel mesh stays frozen mid-wound.
                        if (s.Idle) continue;
                        foreach (var rb in s.RagdollRbs)
                            if (rb != null && rb.IsSleeping()) rb.WakeUp();
                    }
                    else TryStick(s, dt);
                }
                catch (Exception e) { Fault(s, e); }
            }
        }

        /// Retires one syringe after a game API throws, instead of letting the
        /// exception escape into OnUpdate/OnFixedUpdate. A stripped binding once
        /// aborted the whole loop on every physics step: nothing worked at all, and
        /// the only symptom was a stack trace fifty times a second.
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

            var obj = Props.Spawn("FruitLab_Syringe", PropScale, FullColor);
            if (obj == null) return;

            Vector3 muzzle = cam.transform.position;
            Vector3 fwd    = cam.transform.forward;

            obj.transform.position = muzzle + fwd * 0.5f;
            obj.transform.rotation = cam.transform.rotation;

            var s = new Syringe { Obj = obj };
            s.Rb                = obj.AddComponent<Rigidbody>();
            s.Rb.mass           = 0.1f;
            s.Rb.linearVelocity = fwd * Config.ThrowSpeed;

            // Seed the path at the camera, not at the spawn point, so the first stick
            // test covers the half metre the syringe is teleported across. Point-blank
            // throws used to miss because that gap went untested.
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
            // A syringe that has come to rest has already had its contact frames.
            // Without this, one that missed and settled keeps probing forever.
            if (rb.IsSleeping()) return;

            float radius = Mathf.Max(Config.StickRadius, 0.005f);

            // Cover the whole segment from where the syringe was last checked to
            // where this physics step will put it, so nothing falls between frames.
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

        /// The dose is used up: unstick, drop, and stop doing any work. The object
        /// stays as debris until its lifetime runs out or Recall clears it.
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
                // Back the needle out before the collider comes back. It is sitting at
                // the surface it entered, so half its length is inside the limb, and
                // re-enabling in place would have depenetration fling it.
                s.Obj.transform.position -= s.Obj.transform.forward * 0.07f;

                var col = s.Obj.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                Props.Tint(s.Obj, SpentColor);
                s.LastPos = s.Obj.transform.position;
            }

            if (s.Rb != null)
            {
                s.Rb.isKinematic     = false;
                s.Rb.linearVelocity  = Vector3.zero;
                s.Rb.angularVelocity = Vector3.zero;
                s.Rb.WakeUp();      // isKinematic alone does not resume simulation
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Heal wave
        // ══════════════════════════════════════════════════════════════════════════

        /// Builds the limb queue and starts the wave at the impact point.
        private static void StartPass(Syringe s)
        {
            s.Targets.Clear();
            s.RagdollRbs.Clear();
            s.Elapsed   = 0f;
            s.TickAccum = 0f;
            s.Idle      = false;
            s.IdleTimer = 0f;

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

            if (s.Targets.Count > 0)
                MelonLogger.Msg($"[FruitLab] Healing {s.Targets.Count} limb(s) on {root.name}.");
            else if (!s.WarnedEmpty)
            {
                s.WarnedEmpty = true;
                MelonLogger.Warning($"[FruitLab] Stuck to {root.name} but found no healable limbs.");
            }
        }

        /// Advances one syringe's wave. Driven from OnUpdate rather than a coroutine
        /// so healing keeps its own clock instead of stalling whenever WaitForSeconds
        /// is frozen by a paused timeScale.
        private static void TickPass(Syringe s, float dt)
        {
            if (s.Idle)
            {
                s.IdleTimer += dt;
                // A syringe that found nothing to heal backs off to once a second
                // instead of re-walking the creature ten times a second.
                float wait = s.Targets.Count > 0 ? RecheckInterval : Mathf.Max(RecheckInterval, 1f);
                if (s.IdleTimer < wait) return;
                s.IdleTimer = 0f;

                int now = DisabledVoxelTotal(s);
                // -1 means no limb reported shape data, so there is nothing to compare
                // and we fall back to re-sweeping on every recheck.
                if (now < 0 || now != s.DamageWatermark) StartPass(s);
                return;
            }

            if (s.Targets.Count == 0) { FinishPass(s); return; }

            s.TickAccum += dt;
            float interval = Mathf.Max(Config.HealTickInterval, 0.001f);

            // Cap catch-up so a long frame hitch cannot fire dozens of solves at once.
            for (int guard = 0; guard < 4 && s.TickAccum >= interval; guard++)
            {
                s.TickAccum -= interval;
                s.Elapsed   += interval;
                StepWave(s);
            }
            // Drop whatever the guard could not work through, rather than carrying a
            // backlog that keeps the loop pinned at its cap from then on.
            if (s.TickAccum > interval) s.TickAccum = interval;

            foreach (var t in s.Targets) if (!t.Done) return;
            FinishPass(s);
        }

        private static void FinishPass(Syringe s)
        {
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

        /// Heals the voxels whose distance from the impact point falls in
        /// (inner, outer]. Sweeping the grid shell by shell covers every voxel in the
        /// limb exactly once. The original walked a sphere capped at the largest grid
        /// dimension, which never reached the far corners and re-sent the whole
        /// accumulated interior on every tick.
        private static void EmitShell(Target t, float inner, float outer)
        {
            float innerSq = inner * inner;
            float outerSq = outer * outer;
            // The first shell starts at zero, and a half-open (inner, outer] test
            // would drop the impact voxel itself.
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

                // PatchOrganDestroyLVA keeps this creature's organs alive for the
                // duration of the pass, so the solve inside Send cannot undo the heal.
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

        /// Furthest corner of the grid from the impact index, so the wave has a finish
        /// line that actually covers the limb.
        private static float CornerRadius(Vector3Int o, int len, int hgt, int wid)
        {
            int dx = Math.Max(o.x, len - 1 - o.x);
            int dy = Math.Max(o.y, hgt - 1 - o.y);
            int dz = Math.Max(o.z, wid - 1 - o.z);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// Total destroyed voxels across the queued limbs, or -1 if no limb reports
        /// shape data. Used as a damage watermark: while it holds steady there is
        /// nothing new to heal, so a refillable syringe idles instead of re-sweeping
        /// every limb ten times a second.
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

        /// Cheap gate, so the patch costs nothing when no syringe is working.
        public static bool AnyPassRunning
        {
            get
            {
                foreach (var s in _live)
                    if (s.Stuck && !s.Idle && s.CreatureRoot != null) return true;
                return false;
            }
        }

        /// True if <paramref name="organ"/> belongs to a creature currently being
        /// healed, in which case its LVA teardown is held off until the pass ends.
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
