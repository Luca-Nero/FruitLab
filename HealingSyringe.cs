using Il2Cpp;
using Il2CppEffectors;
using Il2CppEffectors.ReceiveMethods.Index;
using Il2CppInterop.Runtime;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using Il2CppVoxelMeshGeneration;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FruitLab
{
    internal enum OrganLayer
    {
        Internal = 0,
        Muscle   = 1,
        Skin     = 2,
        Other    = 3,
    }

    internal class HealTarget
    {
        public LimbEffectorReceiver Ler;
        public Vector3              SamplePos;
        public OrganLayer           Layer;
        public string               Label;
        public Vector3Int           VoxelOrigin;
        public int                  VoxelMax;
        public float                StartDelay;
        public float                WaveRadius;
        public bool                 Done;
    }

    internal class SyringeState
    {
        public GameObject       Obj;
        public bool             Stuck;
        public Rigidbody        HostRb;
        public Vector3          LocalOffset;
        public Quaternion       LocalRotation;
        public float            Timer;
        public List<HealTarget> HealQueue  = new List<HealTarget>();
        public float            HealElapsed;
        public Transform        RagdollRoot;
        public List<Rigidbody>  RagdollRbs = new List<Rigidbody>();
    }

    internal static class HealingSyringe
    {
        private static readonly List<SyringeState> _syringes = new List<SyringeState>();
        private static Mesh _syringeMesh;

        // ── Public API ────────────────────────────────────────────────────────────

        public static void Throw()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var s = new SyringeState();
            s.Obj = BuildSyringeObj();
            if (s.Obj == null) return;

            s.Obj.transform.position = cam.transform.position + cam.transform.forward * 0.5f;
            s.Obj.transform.rotation = cam.transform.rotation;

            var rb            = s.Obj.AddComponent<Rigidbody>();
            rb.mass           = 0.1f;
            rb.linearVelocity = cam.transform.forward * Config.ThrowSpeed;

            _syringes.Add(s);
        }

        public static void RecallAll()
        {
            foreach (var s in _syringes)
            {
                s.Stuck = false; // exits any running HealRoutine on next iteration
                if (s.Obj != null) UnityEngine.Object.Destroy(s.Obj);
            }
            _syringes.Clear();
            FruitLab.Core.KeepAwake    = false;
            FruitLab.Core.HealingActive = false;
            MelonLogger.Msg($"[DoNoHarm] Recalled. KeepAwake={FruitLab.Core.KeepAwake}, HealingActive={FruitLab.Core.HealingActive}");
        }

        /// Updates stuck syringe transforms to follow their host rigidbody.
        /// Also prunes destroyed syringes from the list.
        public static void UpdatePositions()
        {
            for (int i = _syringes.Count - 1; i >= 0; i--)
            {
                var s = _syringes[i];
                if (s.Obj == null)
                {
                    _syringes.RemoveAt(i);
                    if (_syringes.Count == 0) FruitLab.Core.KeepAwake = false;
                    continue;
                }

                if (s.Stuck && s.HostRb != null)
                {
                    s.Obj.transform.position = s.HostRb.transform.TransformPoint(s.LocalOffset);
                    s.Obj.transform.rotation = s.HostRb.transform.rotation * s.LocalRotation;
                }
            }
        }

        /// Called from OnFixedUpdate. Handles physics stick detection and keep-awake.
        public static void FixedTick()
        {
            for (int i = _syringes.Count - 1; i >= 0; i--)
            {
                var s = _syringes[i];
                if (s.Obj == null) { _syringes.RemoveAt(i); continue; }

                if (s.Stuck)
                {
                    foreach (var rb in s.RagdollRbs)
                        if (rb != null && rb.IsSleeping()) rb.WakeUp();
                }
                else
                {
                    s.Timer += Time.fixedDeltaTime;
                    TryStickFixed(s, Time.fixedDeltaTime);
                }
            }
        }

        // ── Stick ────────────────────────────────────────────────────────────────

        private static void TryStickFixed(SyringeState s, float dt)
        {
            var rb = s.Obj.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) return;

            Vector3 vel   = rb.linearVelocity;
            float   speed = vel.magnitude;
            if (speed < 0.15f) return;
            if (s.Timer < 0.01f) return;

            float   castDist = Mathf.Max(speed * dt + 0.03f, 0.05f);
            Vector3 dir      = vel / speed;

            if (!Physics.SphereCast(
                    rb.position, 0.02f, dir, out RaycastHit hit,
                    castDist, ~0, QueryTriggerInteraction.Ignore))
                return;

            if (hit.collider == null || hit.collider.gameObject == s.Obj) return;
            if (!IsLimb(hit.collider.gameObject)) return;

            var hostRb = hit.collider.attachedRigidbody;
            if (hostRb == null) return;

            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;

            s.Obj.transform.position = hit.point;
            s.Obj.transform.rotation = Quaternion.LookRotation(dir);

            s.Stuck         = true;
            s.HostRb        = hostRb;
            s.LocalOffset   = hostRb.transform.InverseTransformPoint(hit.point);
            s.LocalRotation = Quaternion.Inverse(hostRb.transform.rotation) * s.Obj.transform.rotation;
            s.RagdollRoot   = hit.collider.transform.root;

            var col = s.Obj.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            BuildQueue(s);
            FireWakeNudge(s);
            FruitLab.Core.KeepAwake = true;
            MelonLogger.Msg($"[DoNoHarm] Stuck — {s.HealQueue.Count} limbs queued.");

            MelonCoroutines.Start(HealRoutine(s));
        }

        // ── Heal coroutine ───────────────────────────────────────────────────────

        private static IEnumerator HealRoutine(SyringeState s)
        {
            var wait = new WaitForSeconds(Config.HealTickInterval);
            while (s.Obj != null && s.Stuck)
            {
                if (s.HealQueue.TrueForAll(t => t.Done))
                {
                    yield return new WaitForSeconds(Config.RecheckInterval);
                    BuildQueue(s);
                    continue;
                }

                s.HealElapsed += Config.HealTickInterval;

                foreach (var cur in s.HealQueue)
                {
                    if (cur.Done || s.HealElapsed < cur.StartDelay) continue;

                    ForceWakeZw(cur.Ler);
                    cur.WaveRadius += Config.HealWaveSpeed;

                    var builder = MakeBuilder(cur, cur.WaveRadius);
                    if (builder == null) { cur.Done = true; continue; }

                    FruitLab.Core.HealingActive = true;

                    try { cur.Ler.cyn(new bjb<bit>(builder)); }
                    finally { FruitLab.Core.HealingActive = false; builder.Dispose(); }


                    if (cur.WaveRadius >= cur.VoxelMax)
                        cur.Done = true;
                }

                yield return wait;
            }
        }

        // ── Queue ────────────────────────────────────────────────────────────────

        private static void BuildQueue(SyringeState s)
        {
            s.HealQueue.Clear();
            s.HealElapsed = 0f;
            s.RagdollRbs.Clear();

            if (s.RagdollRoot == null || s.Obj == null) return;

            Vector3 origin = s.Obj.transform.position;
            var temp = new List<(HealTarget t, float dist)>();

            for (int i = 0; i < s.RagdollRoot.childCount; i++)
            {
                var prefab = s.RagdollRoot.GetChild(i);

                var physicsT = prefab.Find("Physics");
                if (physicsT == null) continue;

                var ler = physicsT.GetComponent<LimbEffectorReceiver>();
                if (ler == null || ler.wtb == null) continue;

                var rb = physicsT.GetComponent<Rigidbody>();
                if (rb != null) { if (!s.RagdollRbs.Contains(rb)) s.RagdollRbs.Add(rb); rb.WakeUp(); }

                var organsT      = prefab.Find("Organs");
                var physicsCol   = physicsT.GetComponentInChildren<Collider>();
                var physicsSample = physicsCol != null ? physicsCol.bounds.center : physicsT.position;

                float limbDist = float.MaxValue;
                if (organsT != null)
                    for (int j = 0; j < organsT.childCount; j++)
                        limbDist = Mathf.Min(limbDist, Vector3.Distance(organsT.GetChild(j).position, origin));
                else
                    limbDist = Vector3.Distance(physicsSample, origin);

                var voxOrigin = Vector3Int.zero;
                int voxMax    = 16;
                try
                {
                    voxOrigin = ct.djh(ler.wtb, origin);
                    var v     = ler.wtb.pjw;
                    voxMax    = Math.Max(v.length, Math.Max(v.height, v.width));
                    voxOrigin.x = Math.Clamp(voxOrigin.x, 0, v.length - 1);
                    voxOrigin.y = Math.Clamp(voxOrigin.y, 0, v.height - 1);
                    voxOrigin.z = Math.Clamp(voxOrigin.z, 0, v.width - 1);
                }
                catch { }

                temp.Add((
                    new HealTarget {
                        Ler         = ler,
                        SamplePos   = physicsSample,
                        Layer       = OrganLayer.Other,
                        Label       = prefab.name,
                        VoxelOrigin = voxOrigin,
                        VoxelMax    = voxMax,
                        StartDelay  = limbDist / Config.HealWorldSpeed,
                        WaveRadius  = 0f,
                        Done        = false,
                    },
                    limbDist
                ));
            }

            temp.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var (t, _) in temp)
                s.HealQueue.Add(t);

            MelonLogger.Msg($"[DoNoHarm] BuildQueue: {s.HealQueue.Count} limb(s) queued");
        }

        // ── Voxel builder ────────────────────────────────────────────────────────

        private static bjd MakeBuilder(HealTarget t, float waveRadius)
        {
            var ler = t.Ler;
            if (ler == null || ler.wtb == null) return null;

            VoxelMesh.Voxels voxels;
            try { voxels = ler.wtb.pjw; }
            catch { return null; }

            int len = voxels.length, hgt = voxels.height, wid = voxels.width;
            if (len <= 0 || hgt <= 0 || wid <= 0) return null;

            var origin = t.VoxelOrigin;
            int r2 = (int)(waveRadius * waveRadius); // int since deltas are int

            int xMin = Math.Max(0, origin.x - (int)waveRadius - 1);
            int xMax = Math.Min(len, origin.x + (int)waveRadius + 2);
            int yMin = Math.Max(0, origin.y - (int)waveRadius - 1);
            int yMax = Math.Min(hgt, origin.y + (int)waveRadius + 2);
            int zMin = Math.Max(0, origin.z - (int)waveRadius - 1);
            int zMax = Math.Min(wid, origin.z + (int)waveRadius + 2);

            int r = (int)waveRadius + 2;
            var signals = new List<IndexEffectorSignal>(r * r * r);

            for (int z = zMin; z < zMax; z++)
                for (int y = yMin; y < yMax; y++)
                {
                    int dy = y - origin.y, dz = z - origin.z;
                    int dydz = dy * dy + dz * dz;
                    if (dydz > r2) continue;
                    for (int x = xMin; x < xMax; x++)
                    {
                        int dx = x - origin.x;
                        if (dx * dx + dydz <= r2)
                            signals.Add(new IndexEffectorSignal(
                                fp.ecz(new Vector3Int(x, y, z)),
                                Config.HealSignal,
                                InfluenceProcessType.Equate));
                    }
                }

            if (signals.Count == 0) return null;
            var builder = new bjd(signals.Count, false);
            foreach (var s in signals) builder.jbq(s);
            return builder;
        }

        // ── Wake helpers ─────────────────────────────────────────────────────────

        private static void FireWakeNudge(SyringeState s)
        {
            foreach (var t in s.HealQueue)
            {
                try
                {
                    var rb = t.Ler?.GetComponentInParent<Rigidbody>();
                    if (rb == null) continue;
                    rb.WakeUp();

                    var birComp = rb.GetComponent(Il2CppType.Of<bir>());
                    if (birComp == null) continue;

                    var biwReceiver = birComp.TryCast<biw>();
                    if (biwReceiver == null) continue;

                    var builder = new bjd(1, false);
                    builder.jbq(new IndexEffectorSignal(
                        fp.ecz(t.VoxelOrigin),
                        1f,
                        (InfluenceProcessType)1));

                    try { biwReceiver.cyn(new bjb<bit>(builder)); }
                    finally { builder.Dispose(); }
                    MelonLogger.Msg($"Wake nudge fired for {t.Label}");
                }
                catch { }
            }
        }

        private static void ForceWakeZw(LimbEffectorReceiver ler)
        {
            try
            {
                var zw = ler.m_limbReferences?.xke?.sks;
                if (zw != null) zw.skf = true;
            }
            catch { }
        }

        // ── GameObject / mesh helpers ─────────────────────────────────────────────

        private static bool IsLimb(GameObject obj) =>
            obj.GetComponentInParent(Il2CppType.Of<LimbEffectorReceiver>()) != null;

        private static GameObject BuildSyringeObj()
        {
            var obj = new GameObject("DoNoHarm_Syringe");
            var mf  = obj.AddComponent<MeshFilter>();
            var mr  = obj.AddComponent<MeshRenderer>();

            if (_syringeMesh != null)
            {
                mf.mesh = _syringeMesh;
                obj.transform.localScale = Vector3.one * 0.1f;
            }
            else
            {
                mf.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                obj.transform.localScale = new Vector3(0.015f, 0.015f, 0.12f);
            }

            Shader shader = null;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                if (r?.sharedMaterial?.shader != null) { shader = r.sharedMaterial.shader; break; }

            if (shader != null)
            {
                mr.material       = new Material(shader);
                mr.material.color = new Color(0.75f, 0.93f, 1f, 1f);
            }
            mr.shadowCastingMode = ShadowCastingMode.Off;

            obj.AddComponent<BoxCollider>();
            return obj;
        }
    }
}
