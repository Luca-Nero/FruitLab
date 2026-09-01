using Il2CppEffectors;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using Il2CppVoxelMeshGeneration;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Flesh Rot Syringe — a self-contained FruitLab item.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class RotSyringe
    {
        public const string DisplayName = "Flesh Rot Syringe";

        public  static readonly Color IconColor = new Color(0.45f, 0.13f, 0.27f, 1f);
        private static readonly Color SpentColor = new Color(0.22f, 0.14f, 0.17f, 1f);
        private static readonly Vector3 PropScale = new Vector3(0.015f, 0.015f, 0.12f);

        private static readonly Color32 RotTint  = new Color32(96, 28, 52, 255);
        private static readonly Color32 DeadTint = new Color32(20, 13, 15, 255);

        private const int RebuildEvery = 2;

        private const int MaxMeshMisses = 24;

        private const int SeatSearch = 4;

        private const float ClaimDelay  = 0.15f;
        private const float ClaimWindow = 1.5f;

        private static readonly HashSet<int>               _scanIds  = new HashSet<int>();
        private static readonly List<LimbEffectorReceiver> _scanLers = new List<LimbEffectorReceiver>();

        private static readonly List<Dose> _live = new List<Dose>();

        private sealed class Infection
        {
            public LimbEffectorReceiver Ler;
            public VoxelMesh            Mesh;
            public int                  Len, Hgt, Wid;

            public float[]              Marked;
            public List<Vector3Int>     Edge     = new List<Vector3Int>();
            public List<Vector3Int>     Rotting  = new List<Vector3Int>();
            public List<Vector3Int>     Dying    = new List<Vector3Int>();

            public int                  Seams;

            public float                StartAt;
            public int                  SincePaint;
            public int                  Misses;
            public bool                 Done;

            public int At(int x, int y, int z) => (z * Hgt + y) * Len + x;

            public bool Inside(int x, int y, int z) =>
                x >= 0 && y >= 0 && z >= 0 && x < Len && y < Hgt && z < Wid;
        }

        private sealed class Split
        {
            public int              Source;
            public Vector3          Centre;
            public float            Radius;
            public HashSet<int>     Before;
            public float            DueAt, ExpiresAt;
        }

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
            public bool  Faulted;

            public Transform    CreatureRoot;
            public Limbs.Graph  Graph;
            public Infection[]  Limbs;
            public List<Split>  Splits = new List<Split>();
            public float        Elapsed;
            public float        TickAccum;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loops
        // ══════════════════════════════════════════════════════════════════════════

        public static void Register()
        {
            if (Config.RotEnabled) Dismemberment.Split += OnLimbSplit;
        }

        public static void OnSceneReload() => _live.Clear();

        public static void OnUpdate()
        {
            if (_live.Count == 0) return;
            float dt = Time.deltaTime;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var d = _live[i];
                if (d.Obj == null) { _live.RemoveAt(i); continue; }

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
                    UnityEngine.Object.Destroy(d.Obj);
                    _live.RemoveAt(i);
                    continue;
                }

                try
                {
                    var host = d.HostRb.transform;
                    d.Obj.transform.position = host.TransformPoint(d.LocalOffset);
                    d.Obj.transform.rotation = host.rotation * d.LocalRotation;

                    TickRot(d, dt);
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
                if (d.Obj == null) { _live.RemoveAt(i); continue; }
                if (d.Spent || d.Stuck) continue;

                try { TryStick(d, dt); }
                catch (Exception e) { Fault(d, e); }
            }
        }

        private static void Fault(Dose d, Exception e)
        {
            if (!d.Faulted)
            {
                d.Faulted = true;
                MelonLogger.Warning($"[FruitLab] Rot dose retired after an error: {e.Message}");
            }

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

            var obj = SyringeModel.Spawn("FruitLab_Rot", PropScale, IconColor);
            if (obj == null) return;

            Vector3 muzzle = cam.transform.position;
            Vector3 fwd    = cam.transform.forward;

            obj.transform.position = muzzle + fwd * 0.5f;
            obj.transform.rotation = cam.transform.rotation;

            var d = new Dose { Obj = obj };
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
                d.Stuck = false;
                if (d.Obj != null) UnityEngine.Object.Destroy(d.Obj);
            }
            _live.Clear();
            MelonLogger.Msg($"[FruitLab] Recalled {n} rot dose(s).");
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

            Seed(d, point);
        }

        private static void Spend(Dose d)
        {
            d.Stuck        = false;
            d.Spent        = true;
            d.SpentFor     = 0f;
            d.HostRb       = null;
            d.HostLer      = null;
            d.CreatureRoot = null;
            d.Graph        = null;
            d.Limbs        = null;
            d.Splits.Clear();

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
        // Spread
        // ══════════════════════════════════════════════════════════════════════════

        private static void Seed(Dose d, Vector3 entry)
        {
            d.CreatureRoot = Limbs.CreatureRootOf(d.HostLer);
            if (d.CreatureRoot == null) { Spend(d); return; }

            d.Graph = Limbs.BuildGraph(d.CreatureRoot);
            if (d.Graph.Count == 0) { Spend(d); return; }

            d.Limbs   = new Infection[d.Graph.Count];
            d.Elapsed = 0f;

            int start = d.Graph.IndexOf(d.HostLer);
            if (start < 0) { Spend(d); return; }

            if (Infect(d, start, entry, 0f) == null) { Spend(d); return; }

            MelonLogger.Msg(
                $"[FruitLab] Rot set in on {d.CreatureRoot.name} " +
                $"({d.Graph.Limbs[start].transform.parent?.name ?? "limb"}).");
        }

        private static Infection Infect(Dose d, int index, Vector3 entry, float startAt)
        {
            if (index < 0 || index >= d.Limbs.Length || d.Limbs[index] != null) return d.Limbs[index];

            var ler = d.Graph.Limbs[index];
            var mesh = Limbs.MeshOf(ler);
            if (mesh == null) return null;
            if (!Limbs.TryGetGrid(mesh, out int len, out int hgt, out int wid)) return null;
            if (!Limbs.TryVoxelIndex(mesh, entry, len, hgt, wid, out Vector3Int origin)) return null;

            var inf = new Infection
            {
                Ler     = ler,
                Mesh    = mesh,
                Len     = len, Hgt = hgt, Wid = wid,
                Marked  = new float[len * hgt * wid],
                Seams   = index,
                StartAt = startAt,
            };

            for (int i = 0; i < inf.Marked.Length; i++) inf.Marked[i] = -1f;

            if (!Seat(inf, origin, startAt, out Vector3Int seat))
            {
                Diag.Log("rot", $"{Diag.Name(ler)} has nothing at {origin} to infect");
                return null;
            }

            d.Limbs[index] = inf;

            Diag.Log("rot",
                $"{Diag.Name(ler)} infected — grid {len}x{hgt}x{wid}, took hold at {seat}" +
                (startAt > 0f ? $", starting at {startAt:0.0}s" : ""));

            return inf;
        }

        private static bool Seat(Infection inf, Vector3Int at, float now, out Vector3Int seat)
        {
            seat = at;

            VoxelMesh.Voxels voxels;
            try { voxels = inf.Mesh.pjw; } catch { return false; }

            for (int r = 0; r <= SeatSearch; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dy) != r && Mathf.Abs(dz) != r)
                                continue;

                            int x = at.x + dx, y = at.y + dy, z = at.z + dz;
                            if (!inf.Inside(x, y, z)) continue;

                            try { if (!voxels[x, y, z].enabled) continue; } catch { continue; }

                            seat = new Vector3Int(x, y, z);
                            if (!Take(inf, voxels, x, y, z, now)) continue;

                            inf.Edge.Add(seat);
                            return true;
                        }
            }

            return false;
        }

        private static bool Take(Infection inf, VoxelMesh.Voxels voxels, int x, int y, int z,
                                 float now)
        {
            int i = inf.At(x, y, z);
            if (inf.Marked[i] >= 0f) return false;

            inf.Marked[i] = now;
            inf.Rotting.Add(new Vector3Int(x, y, z));

            Limbs.Paint(inf.Mesh, voxels, x, y, z, RotTint);

            if (Config.RotMarkDamage > 0f)
            {
                _marked.Clear();
                _marked.Add(new Vector3Int(x, y, z));
                Signal(inf, _marked, Config.RotMarkDamage, "rot mark");
            }

            return true;
        }

        private static readonly List<Vector3Int> _marked = new List<Vector3Int>();

        // ══════════════════════════════════════════════════════════════════════════
        // Spread
        // ══════════════════════════════════════════════════════════════════════════

        private static readonly List<int> _neighbours = new List<int>();

        private static void Spread(Dose d, int index, Infection inf)
        {
            d.Graph.Neighbours(index, _neighbours);

            foreach (int n in _neighbours)
            {
                if (n < 0 || n >= d.Limbs.Length || d.Limbs[n] != null) continue;

                Vector3 junction = d.Graph.Junction(index, n);
                if (!Limbs.TryVoxelIndex(inf.Mesh, junction, inf.Len, inf.Hgt, inf.Wid,
                                         out Vector3Int seam))
                    continue;

                if (!Reached(inf, seam)) continue;

                Infect(d, n, junction, d.Elapsed + Config.RotSpreadSeconds);
            }
        }

        private static void OpenSeams(Dose d, int index)
        {
            d.Graph.Neighbours(index, _neighbours);

            foreach (int n in _neighbours)
            {
                if (n < 0 || n >= d.Limbs.Length || d.Limbs[n] != null) continue;
                Infect(d, n, d.Graph.Junction(index, n), d.Elapsed + Config.RotSpreadSeconds);
            }
        }

        private static bool Reached(Infection inf, Vector3Int seam)
        {
            int reach = Mathf.Max(Config.RotSeamReach, 1);

            for (int dz = -reach; dz <= reach; dz++)
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int x = seam.x + dx, y = seam.y + dy, z = seam.z + dz;
                        if (!inf.Inside(x, y, z)) continue;
                        if (inf.Marked[inf.At(x, y, z)] >= 0f) return true;
                    }

            return false;
        }

        private static bool Neighbourhood(LimbEffectorReceiver ler, out Vector3 centre,
                                          out float radius)
        {
            centre = default; radius = 0f;
            try
            {
                var col = ler.GetComponentInChildren<Collider>();
                if (col != null)
                {
                    centre = col.bounds.center;
                    radius = col.bounds.extents.magnitude + 0.3f;
                }
                else
                {
                    centre = ler.transform.position;
                    radius = 0.5f;
                }
                return true;
            }
            catch { return false; }
        }

        private static void ScanLimbs(Vector3 centre, float radius, HashSet<int> ids,
                                      List<LimbEffectorReceiver> lers)
        {
            Limbs.ScanNearby(centre, radius, _scanLers);

            ids.Clear();
            foreach (var ler in _scanLers) ids.Add(ler.GetInstanceID());

            if (lers == null || ReferenceEquals(lers, _scanLers)) return;

            lers.Clear();
            lers.AddRange(_scanLers);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Severed pieces
        // ══════════════════════════════════════════════════════════════════════════

        private static void OnLimbSplit(LimbEffectorReceiver ler)
        {
            if (ler == null) return;

            foreach (var d in _live)
            {
                if (d.Spent || !d.Stuck || d.Limbs == null || d.Graph == null) continue;

                int i = d.Graph.IndexOf(ler);
                if (i < 0 || i >= d.Limbs.Length || d.Limbs[i] == null) continue;

                if (!Neighbourhood(ler, out Vector3 centre, out float radius)) continue;

                var before = new HashSet<int>();
                ScanLimbs(centre, radius, before, null);

                d.Splits.Add(new Split
                {
                    Source    = i,
                    Centre    = centre,
                    Radius    = radius,
                    Before    = before,
                    DueAt     = d.Elapsed + ClaimDelay,
                    ExpiresAt = d.Elapsed + ClaimWindow,
                });
            }
        }

        private static void ClaimPieces(Dose d)
        {
            for (int i = d.Splits.Count - 1; i >= 0; i--)
            {
                var split = d.Splits[i];
                if (d.Elapsed < split.DueAt) continue;
                if (d.Elapsed > split.ExpiresAt) { d.Splits.RemoveAt(i); continue; }

                var source = split.Source < d.Limbs.Length ? d.Limbs[split.Source] : null;
                if (source == null) { d.Splits.RemoveAt(i); continue; }

                ScanLimbs(split.Centre, split.Radius, _scanIds, _scanLers);

                foreach (var ler in _scanLers)
                {
                    if (split.Before.Contains(ler.GetInstanceID())) continue;
                    if (d.Graph.IndexOf(ler) >= 0) continue;

                    split.Before.Add(ler.GetInstanceID());
                    Claim(d, split.Source, source, ler);
                }
            }
        }

        private static void Claim(Dose d, int sourceIndex, Infection source,
                                  LimbEffectorReceiver ler)
        {
            var mesh = Limbs.MeshOf(ler);
            if (mesh == null) return;
            if (!Limbs.TryGetGrid(mesh, out int len, out int hgt, out int wid)) return;

            if (len != source.Len || hgt != source.Hgt || wid != source.Wid) return;

            int flesh = Limbs.EnabledVoxels(ler);
            if (flesh >= 0 && flesh <= Config.RotMinPiece) return;

            var inf = new Infection
            {
                Ler     = ler,
                Mesh    = mesh,
                Len     = len, Hgt = hgt, Wid = wid,
                Marked  = (float[])source.Marked.Clone(),
                Seams   = source.Seams,
                StartAt = d.Elapsed,
            };

            VoxelMesh.Voxels voxels;
            try { voxels = mesh.pjw; } catch { return; }

            float blackenAt = Mathf.Max(Config.RotBlackenAfter, 0f);
            int carried = 0;

            for (int z = 0; z < wid; z++)
                for (int y = 0; y < hgt; y++)
                    for (int x = 0; x < len; x++)
                    {
                        float mark = inf.Marked[inf.At(x, y, z)];
                        if (mark < 0f) continue;

                        try { if (!voxels[x, y, z].enabled) continue; } catch { continue; }

                        var cell = new Vector3Int(x, y, z);
                        carried++;

                        if (d.Elapsed - mark >= blackenAt)
                        {
                            inf.Dying.Add(cell);
                            Limbs.Paint(mesh, voxels, x, y, z, DeadTint);
                        }
                        else
                        {
                            inf.Rotting.Add(cell);
                            Limbs.Paint(mesh, voxels, x, y, z, RotTint);
                        }
                    }

            if (carried == 0) return;

            inf.Edge.AddRange(inf.Rotting);
            inf.Edge.AddRange(inf.Dying);

            int index = d.Graph.Append(ler, sourceIndex);
            Array.Resize(ref d.Limbs, d.Graph.Count);
            d.Limbs[index] = inf;

            Limbs.RebuildMesh(mesh);

            Diag.Log("rot",
                $"claimed {Diag.Name(ler)} off {Diag.Name(source.Ler)}, carrying {carried} " +
                "rotted voxel(s) across");
        }

        private static void ReportSeams(Dose d, int index, Infection inf)
        {
            if (!Diag.On) return;

            d.Graph.Neighbours(index, _neighbours);

            var mine = d.Graph.Limbs[index];
            foreach (int n in _neighbours)
            {
                if (n < 0 || n >= d.Graph.Count) continue;

                Diag.Collision("rotted through", mine, d.Graph.Limbs[n]);

                if (n < d.Limbs.Length && d.Limbs[n] != null) continue;

                Vector3 junction = d.Graph.Junction(index, n);

                if (!Limbs.TryVoxelIndex(inf.Mesh, junction, inf.Len, inf.Hgt, inf.Wid,
                                         out Vector3Int seam))
                {
                    Diag.Log("rot",
                        $"seam to {Diag.Name(d.Graph.Limbs[n])} could not be placed in " +
                        $"{Diag.Name(mine)} at all");
                    continue;
                }

                Diag.Log("rot",
                    $"seam to {Diag.Name(d.Graph.Limbs[n])} sits at {seam} in a " +
                    $"{inf.Len}x{inf.Hgt}x{inf.Wid} grid; nearest rot got {Nearest(inf, seam)} " +
                    $"voxel(s) away, and {Marks(inf)} voxel(s) were infected in all");
            }
        }

        private static int Nearest(Infection inf, Vector3Int to)
        {
            int best = int.MaxValue;

            for (int z = 0; z < inf.Wid; z++)
                for (int y = 0; y < inf.Hgt; y++)
                    for (int x = 0; x < inf.Len; x++)
                    {
                        if (inf.Marked[inf.At(x, y, z)] < 0f) continue;

                        int dist = Mathf.Max(Mathf.Abs(x - to.x),
                                   Mathf.Max(Mathf.Abs(y - to.y), Mathf.Abs(z - to.z)));
                        if (dist < best) best = dist;
                    }

            return best;
        }

        private static int Marks(Infection inf)
        {
            int n = 0;
            foreach (float m in inf.Marked) if (m >= 0f) n++;
            return n;
        }

        private static void TickRot(Dose d, float dt)
        {
            if (d.Limbs == null) { Spend(d); return; }

            d.Elapsed   += dt;
            d.TickAccum += dt;

            float interval = Mathf.Max(Config.RotTickInterval, 0.02f);
            if (d.TickAccum < interval) return;
            d.TickAccum = 0f;

            bool anyAlive = false;

            for (int i = 0; i < d.Limbs.Length; i++)
            {
                var inf = d.Limbs[i];
                if (inf == null) continue;
                if (d.Elapsed < inf.StartAt) { anyAlive = true; continue; }
                if (inf.Done) continue;

                anyAlive = true;
                Advance(d, i, inf);
            }

            if (d.Splits.Count > 0) { ClaimPieces(d); anyAlive = true; }

            if (!anyAlive)
            {
                int reached = 0;
                foreach (var inf in d.Limbs) if (inf != null) reached++;
                Diag.Log("rot", $"ran its course after reaching {reached} limb(s)");

                MelonLogger.Msg("[FruitLab] Rot has run its course.");
                Spend(d);
            }
        }

        private static void Advance(Dose d, int index, Infection inf)
        {
            float now = d.Elapsed;

            VoxelMesh.Voxels voxels;
            try { voxels = inf.Mesh.pjw; }
            catch
            {
                inf.Mesh = Limbs.MeshOf(inf.Ler);
                if (inf.Mesh == null || ++inf.Misses > MaxMeshMisses) inf.Done = true;
                return;
            }
            inf.Misses = 0;

            int painted = Creep(inf, voxels, now);
            painted += Blacken(inf, voxels, now);

            if (painted > 0 && ++inf.SincePaint >= RebuildEvery)
            {
                inf.SincePaint = 0;
                Limbs.RebuildMesh(inf.Mesh);
            }

            Reap(inf, now);
            Spread(d, inf.Seams, inf);

            if (!inf.Done && inf.Edge.Count == 0 && inf.Rotting.Count == 0 && inf.Dying.Count == 0)
            {
                inf.Done = true;
                Diag.Log("rot", $"{Diag.Name(inf.Ler)} is spent");

                OpenSeams(d, inf.Seams);

                ReportSeams(d, inf.Seams, inf);
            }
        }

        private static readonly List<Vector3Int> _grown = new List<Vector3Int>();

        private static int Creep(Infection inf, VoxelMesh.Voxels voxels, float now)
        {
            if (inf.Edge.Count == 0) return 0;

            _grown.Clear();
            int before = inf.Rotting.Count;

            foreach (var cell in inf.Edge)
            {
                for (int face = 0; face < 6; face++)
                {
                    int x = cell.x + (face == 0 ? 1 : face == 1 ? -1 : 0);
                    int y = cell.y + (face == 2 ? 1 : face == 3 ? -1 : 0);
                    int z = cell.z + (face == 4 ? 1 : face == 5 ? -1 : 0);

                    if (!inf.Inside(x, y, z)) continue;
                    if (inf.Marked[inf.At(x, y, z)] >= 0f) continue;

                    bool alive;
                    try { alive = voxels[x, y, z].enabled; } catch { continue; }
                    if (!alive) continue;

                    if (Take(inf, voxels, x, y, z, now)) _grown.Add(new Vector3Int(x, y, z));
                }
            }

            inf.Edge.Clear();
            inf.Edge.AddRange(_grown);

            return inf.Rotting.Count - before;
        }

        private static int Blacken(Infection inf, VoxelMesh.Voxels voxels, float now)
        {
            float after = Mathf.Max(Config.RotBlackenAfter, 0f);
            int painted = 0;

            for (int i = inf.Rotting.Count - 1; i >= 0; i--)
            {
                var cell = inf.Rotting[i];
                if (now - inf.Marked[inf.At(cell.x, cell.y, cell.z)] < after) continue;

                if (Limbs.Paint(inf.Mesh, voxels, cell.x, cell.y, cell.z, DeadTint)) painted++;

                inf.Rotting.RemoveAt(i);
                inf.Dying.Add(cell);
            }

            return painted;
        }

        private static readonly List<Vector3Int> _doomed = new List<Vector3Int>();

        private static void Reap(Infection inf, float now)
        {
            float after = Mathf.Max(Config.RotBlackenAfter, 0f)
                        + Mathf.Max(Config.RotDestroyAfter, 0f);

            _doomed.Clear();

            for (int i = inf.Dying.Count - 1; i >= 0; i--)
            {
                var cell = inf.Dying[i];
                if (now - inf.Marked[inf.At(cell.x, cell.y, cell.z)] < after) continue;

                _doomed.Add(cell);
                inf.Dying.RemoveAt(i);
            }

            Signal(inf, _doomed, Config.RotDamage, "rot");
        }

        private static void Signal(Infection inf, List<Vector3Int> voxels, float amount,
                                   string label)
        {
            if (voxels.Count == 0 || amount <= 0f) return;

            var batch = Limbs.NewBatch(voxels.Count);
            try
            {
                foreach (var v in voxels)
                    Limbs.Add(batch, v.x, v.y, v.z, -Mathf.Abs(amount),
                              InfluenceProcessType.Sum);

                Limbs.Send(inf.Ler, batch, label);
            }
            finally { batch.Dispose(); }
        }

    }
}
