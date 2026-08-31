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
    //
    // The Healing Syringe's opposite number, and deliberately not just a heal wave with
    // the sign flipped. Healing radiates through *space* from one point, reaching every
    // limb at once in proportion to how far away it is. Rot travels through *flesh*: it
    // takes the hand, then the forearm, then the upper arm, then the torso, entering each
    // limb at the joint it arrived through. A syringe in the wrist should never blacken a
    // foot before it has eaten an elbow.
    //
    // Three fronts run per limb, each trailing the last: discolouration ahead of the
    // damage, blackening behind it, and destruction behind that. Blackening and
    // destruction were one front to begin with, which meant a voxel was painted black and
    // switched off in the same tick — the black was never drawn once. Destruction goes
    // through the game's own damage path, so the body bleeds, comes apart and dies of it
    // properly rather than merely losing voxels.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class RotSyringe
    {
        public const string DisplayName = "Flesh Rot Syringe";

        public  static readonly Color IconColor = new Color(0.45f, 0.13f, 0.27f, 1f);
        private static readonly Color SpentColor = new Color(0.22f, 0.14f, 0.17f, 1f);
        private static readonly Vector3 PropScale = new Vector3(0.015f, 0.015f, 0.12f);

        /// Sick purple-red, ahead of the damage; and the dead black that follows it.
        private static readonly Color32 RotTint  = new Color32(96, 28, 52, 255);
        private static readonly Color32 DeadTint = new Color32(20, 13, 15, 255);

        /// Ticks between mesh rebuilds. The paint is already in the voxel array, so this
        /// only decides how promptly it becomes visible — and how often the rebuild's
        /// known throw is provoked.
        private const int RebuildEvery = 2;

        /// Ticks a limb may fail to hand over its voxel grid before it is given up on.
        private const int MaxMeshMisses = 24;

        private static readonly List<Dose> _live = new List<Dose>();

        /// One limb the rot has reached.
        private sealed class Infection
        {
            public LimbEffectorReceiver Ler;
            public VoxelMesh            Mesh;
            public int                  Len, Hgt, Wid;
            public Vector3Int           Origin;      // where the rot entered this limb
            public float                StartAt;     // seconds into the dose
            public float                Front;       // discolouration, leading
            public float                Necrosis;    // blackening, trailing the above
            public float                Death;       // destruction, trailing the above
            public float                MaxRadius;
            public int                  SincePaint;  // ticks since the mesh was rebuilt
            public int                  Misses;      // consecutive failures to read the mesh
            public bool                 Done;
        }

        /// A limb of ours has just come apart, and the pieces are yet to be claimed.
        ///
        /// The pieces are not looked for straight away: a fragment is a limb the game
        /// has only just built, and its collider is not up for a frame or two, so a
        /// scan run on the spot finds nothing. Kept for a window instead and re-scanned
        /// each tick, because one split can drop several pieces and they do not all
        /// arrive together.
        private sealed class Split
        {
            public int              Source;    // index into Dose.Limbs of the limb that split
            public Vector3          Centre;    // taken while the limb was still whole
            public float            Radius;
            public HashSet<int>     Before;    // limbs already in that space, so they are not claimed
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
            public Infection[]  Limbs;          // indexed alongside Graph.Limbs
            public List<Split>  Splits = new List<Split>();
            public float        Elapsed;
            public float        TickAccum;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loops
        // ══════════════════════════════════════════════════════════════════════════

        /// The rack owns this syringe's toolbar slot; the only thing left to wire up
        /// is the severing hook, which has to be live before anything is thrown.
        public static void Register() => Dismemberment.Split += OnLimbSplit;

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

            var obj = Props.Spawn("FruitLab_Rot", PropScale, IconColor);
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
        // Spread
        // ══════════════════════════════════════════════════════════════════════════

        /// Infects the limb the needle went into. Everything else is reached from here,
        /// limb by limb, through the joints.
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
                Ler       = ler,
                Mesh      = mesh,
                Len       = len, Hgt = hgt, Wid = wid,
                Origin    = origin,
                StartAt   = startAt,
                MaxRadius = CornerRadius(origin, len, hgt, wid),
            };

            d.Limbs[index] = inf;
            return inf;
        }

        /// Hands the rot on to whatever this limb is joined to, entering each neighbour at
        /// the joint they share. That entry point is what makes it read as one continuous
        /// creep rather than several unrelated patches: the next limb starts rotting at
        /// the wrist, not at its centre.
        ///
        /// Retried every tick rather than fired once. Infecting a neighbour needs its
        /// voxel grid, and a limb that is busy coming apart at that instant cannot give
        /// one — a single attempt silently dropped it and everything beyond it, so the rot
        /// died at whichever joint happened to be detaching as the front arrived. Which is
        /// exactly where it is most likely to be detaching.
        private static readonly List<int> _neighbours = new List<int>();

        private static void Spread(Dose d, int index)
        {
            d.Graph.Neighbours(index, _neighbours);

            foreach (int n in _neighbours)
            {
                if (n < 0 || n >= d.Limbs.Length || d.Limbs[n] != null) continue;
                Infect(d, n, d.Graph.Junction(index, n), d.Elapsed + Config.RotSpreadSeconds);
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Severed pieces
        //
        // Severing a limb does not wound it — it replaces it. The game separates the
        // voxel mesh and builds a *new* limb for each disconnected group out of the limb
        // prefab (see Dismemberment), and the separated data carries voxel indexes and
        // nothing else. So the piece arrives as untouched flesh under a receiver we have
        // never seen — which is why rot used to stop dead at a severed joint, and why the
        // limb appeared to heal itself back to healthy pink on its way off the body.
        //
        // The piece is the same flesh it was a moment ago, so the rot is handed over
        // rather than restarted: same origin, same three fronts, and the ground already
        // covered is repainted onto it.
        // ══════════════════════════════════════════════════════════════════════════

        /// How long after a split to start looking for the pieces, and how long to keep
        /// looking. A fragment's collider is not up immediately, and one split can drop
        /// several pieces that do not all arrive on the same frame.
        private const float ClaimDelay  = 0.15f;
        private const float ClaimWindow = 1.5f;

        private static readonly HashSet<int>               _scanIds  = new HashSet<int>();
        private static readonly List<LimbEffectorReceiver> _scanLers = new List<LimbEffectorReceiver>();

        private static void OnLimbSplit(LimbEffectorReceiver ler)
        {
            if (ler == null) return;

            foreach (var d in _live)
            {
                if (d.Spent || !d.Stuck || d.Limbs == null || d.Graph == null) continue;

                int i = d.Graph.IndexOf(ler);
                if (i < 0 || i >= d.Limbs.Length || d.Limbs[i] == null) continue;

                // Measured now, while the limb is still whole: by the time the pieces are
                // looked for it may be gone, and there would be nowhere left to look.
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
                    // Anything that was already standing there belongs to somebody else.
                    if (split.Before.Contains(ler.GetInstanceID())) continue;
                    if (d.Graph.IndexOf(ler) >= 0) continue;
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

            Vector3Int origin;
            float front = 0f, necrosis = 0f, death = 0f;

            if (len == source.Len && hgt == source.Hgt && wid == source.Wid)
            {
                // Same grid, so the piece still indexes its voxels the way the limb it
                // came off did: the injection point sits at the same coordinate and every
                // distance already measured still holds. The rot simply carries on.
                origin   = source.Origin;
                front    = source.Front;
                necrosis = source.Necrosis;
                death    = source.Death;
            }
            else
            {
                // A differently shaped grid shares no coordinates, so the only honest
                // common reference is where the injection point is in the world. The
                // distances cannot come across with it, so the piece rots afresh from the
                // side the rot arrived on.
                if (!Limbs.TryIndexToWorld(source.Mesh, source.Origin, out Vector3 world))
                    world = ler.transform.position;
                if (!Limbs.TryVoxelIndex(mesh, world, len, hgt, wid, out origin)) return;
            }

            var inf = new Infection
            {
                Ler       = ler,
                Mesh      = mesh,
                Len       = len, Hgt = hgt, Wid = wid,
                Origin    = origin,
                StartAt   = d.Elapsed,
                Front     = front,
                Necrosis  = necrosis,
                Death     = death,
                MaxRadius = CornerRadius(origin, len, hgt, wid),
            };

            int index = d.Graph.Append(ler, sourceIndex);
            Array.Resize(ref d.Limbs, d.Graph.Count);
            d.Limbs[index] = inf;

            // The piece came out of the prefab as healthy flesh, so everything the rot
            // had already crossed has to be put back on it before the fronts move again.
            if (front > 0f) Backfill(inf);
        }

        /// Repaints the ground the rot has already covered onto a piece that arrived
        /// clean. Destroys nothing: whatever the death front had reached is not in this
        /// piece — that flesh is exactly what was taken to sever it in the first place.
        private static void Backfill(Infection inf)
        {
            VoxelMesh.Voxels voxels;
            try { voxels = inf.Mesh.pjw; } catch { return; }

            float frontSq = inf.Front    * inf.Front;
            float necroSq = inf.Necrosis * inf.Necrosis;
            int   painted = 0;

            for (int z = 0; z < inf.Wid; z++)
                for (int y = 0; y < inf.Hgt; y++)
                {
                    int dy = y - inf.Origin.y, dz = z - inf.Origin.z;
                    int yz = dy * dy + dz * dz;
                    if (yz > frontSq) continue;

                    for (int x = 0; x < inf.Len; x++)
                    {
                        int dx = x - inf.Origin.x;
                        int d2 = dx * dx + yz;

                        if (d2 <= necroSq)
                        {
                            if (Limbs.Paint(inf.Mesh, voxels, x, y, z, DeadTint)) painted++;
                        }
                        else if (d2 <= frontSq)
                        {
                            if (Limbs.Paint(inf.Mesh, voxels, x, y, z, RotTint)) painted++;
                        }
                    }
                }

            if (painted > 0) Limbs.RebuildMesh(inf.Mesh);
        }

        /// Where to look for the pieces of a limb, and how far out.
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

        /// The limbs in a piece of space, by instance id as well as by reference —
        /// the ids are what a before-and-after comparison is made on.
        private static void ScanLimbs(Vector3 centre, float radius, HashSet<int> ids,
                                      List<LimbEffectorReceiver> lers)
        {
            Limbs.ScanNearby(centre, radius, _scanLers);

            ids.Clear();
            foreach (var ler in _scanLers) ids.Add(ler.GetInstanceID());

            // _scanLers is the shared scratch list, so a caller that wants to keep the
            // limbs has to be handed them separately.
            if (lers == null || ReferenceEquals(lers, _scanLers)) return;

            lers.Clear();
            lers.AddRange(_scanLers);
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

            // After the limb pass, never during it: claiming a piece appends to the
            // graph and grows the array being walked.
            if (d.Splits.Count > 0) { ClaimPieces(d); anyAlive = true; }

            if (!anyAlive)
            {
                MelonLogger.Msg("[FruitLab] Rot has run its course.");
                Spend(d);
            }
        }

        private static void Advance(Dose d, int index, Infection inf)
        {
            float prevFront    = inf.Front;
            float prevNecrosis = inf.Necrosis;
            float prevDeath    = inf.Death;

            inf.Front    += Mathf.Max(Config.RotWaveSpeed, 0.01f);
            inf.Necrosis  = Mathf.Max(0f, inf.Front    - Mathf.Max(Config.RotNecrosisLag, 0f));
            inf.Death     = Mathf.Max(0f, inf.Necrosis - Mathf.Max(Config.RotDeathLag, 0f));

            Consume(inf, prevFront, prevNecrosis, prevDeath);

            // Once the discolouration has crossed enough of the limb, the rot is in the
            // joints and the neighbours are next. Seeding on the *leading* front, not on
            // completion, is what keeps it moving as one wave instead of stopping at
            // every border. Spread skips neighbours it has already taken, so calling it
            // every tick simply keeps trying the ones it could not.
            if (inf.Front >= inf.MaxRadius * Config.RotSpreadAt) Spread(d, index);

            if (inf.Death >= inf.MaxRadius) inf.Done = true;
        }

        /// One pass over the limb's grid covering all three fronts.
        ///
        /// Blackening and destruction used to share a shell, so a voxel was painted black
        /// and switched off in the same tick and the black was never once drawn — the
        /// tissue went straight from purple to absent. (Healing a rotted body proved the
        /// paint had landed: the flesh grew back black.) They are separate bands now, and
        /// RotDeathLag is how long dead tissue is left standing before it goes.
        private static void Consume(Infection inf, float prevFront, float prevNecrosis,
                                    float prevDeath)
        {
            // A detaching limb hands out a mesh that is briefly unreadable, and it used to
            // be written off on the first failure — so rot reliably died at the very joint
            // it had just eaten through. Re-resolve and keep trying for a while first.
            VoxelMesh.Voxels voxels;
            try { voxels = inf.Mesh.pjw; }
            catch
            {
                inf.Mesh = Limbs.MeshOf(inf.Ler);
                if (inf.Mesh == null || ++inf.Misses > MaxMeshMisses) inf.Done = true;
                else if (Limbs.TryGetGrid(inf.Mesh, out int len, out int hgt, out int wid))
                {
                    // A replacement mesh need not be the grid we measured against.
                    inf.Len = len; inf.Hgt = hgt; inf.Wid = wid;
                    inf.Origin = new Vector3Int(Math.Clamp(inf.Origin.x, 0, len - 1),
                                                Math.Clamp(inf.Origin.y, 0, hgt - 1),
                                                Math.Clamp(inf.Origin.z, 0, wid - 1));
                }
                return;
            }
            inf.Misses = 0;

            float frontSq = inf.Front    * inf.Front,    prevFrontSq = prevFront    * prevFront;
            float necroSq = inf.Necrosis * inf.Necrosis, prevNecroSq = prevNecrosis * prevNecrosis;
            float deathSq = inf.Death    * inf.Death,    prevDeathSq = prevDeath    * prevDeath;

            var doomed  = new List<Vector3Int>();
            var marked  = new List<Vector3Int>();
            int painted = 0;

            for (int z = 0; z < inf.Wid; z++)
                for (int y = 0; y < inf.Hgt; y++)
                {
                    int dy = y - inf.Origin.y, dz = z - inf.Origin.z;
                    int yz = dy * dy + dz * dz;
                    if (yz > frontSq) continue;

                    for (int x = 0; x < inf.Len; x++)
                    {
                        int dx = x - inf.Origin.x;
                        int d2 = dx * dx + yz;

                        // Innermost band first: the three are disjoint, so each voxel is
                        // discoloured, then blackened, then taken, one band per pass.
                        if (d2 <= deathSq && d2 > prevDeathSq)
                            doomed.Add(new Vector3Int(x, y, z));
                        else if (d2 <= necroSq && d2 > prevNecroSq)
                        {
                            if (Limbs.Paint(inf.Mesh, voxels, x, y, z, DeadTint)) painted++;
                        }
                        else if (d2 <= frontSq && d2 > prevFrontSq)
                        {
                            if (Limbs.Paint(inf.Mesh, voxels, x, y, z, RotTint)) painted++;

                            // A scattering of the discoloured front is taken outright.
                            // Colour alone is invisible: the mesh only re-meshes a chunk
                            // whose topology changed, so painted-but-intact flesh keeps
                            // its old surface until something near it is destroyed — which
                            // is why the rot read as nothing, nothing, nothing, gone. The
                            // pits force those updates, and pitting is what rotting flesh
                            // does anyway.
                            if (Pitted(x, y, z)) doomed.Add(new Vector3Int(x, y, z));
                            else if (Config.RotMarkDamage > 0f) marked.Add(new Vector3Int(x, y, z));
                        }
                    }
                }

            // The rebuild is what makes the paint visible, and there is no substitute.
            //
            // It was removed once on the theory that destroying a voxel would make the
            // game re-mesh that chunk and pick the colours up for free. It does re-mesh —
            // but from its own changed-data map, which only contains voxels the game
            // itself altered, so our painted neighbours are never in it. Removing this
            // cost the colour entirely and fixed nothing it was blamed for.
            //
            // Throttled because a rebuild is whole-limb: doing it every tick of every
            // rotting limb is the expensive part of the effect rather than the effect.
            if (painted > 0 && ++inf.SincePaint >= RebuildEvery)
            {
                inf.SincePaint = 0;
                Limbs.RebuildMesh(inf.Mesh);
            }

            // Two signal batches, deliberately different in kind.
            //
            // The leading front only *marks* tissue: a token amount that registers the rot
            // in the organs' effector collectors without taking anything, so the body is
            // already dying of it while the flesh is still standing. Turn RotMarkDamage up
            // and the creature will feel the rot spreading rather than only its aftermath.
            Signal(inf, marked, Config.RotMarkDamage, "rot mark");
            Signal(inf, doomed, Config.RotDamage,     "rot");
        }

        /// Destruction goes through the game's own damage path rather than simply
        /// switching voxels off, so the body bleeds from it, comes apart at the seams it
        /// should, and dies of the rot properly.
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

        /// Whether this voxel is one of the pits. Hashed from the coordinate rather than
        /// drawn at random so a given voxel is always either a pit or not — a coin toss
        /// per tick would make the front shimmer as it re-evaluated the same flesh.
        private static bool Pitted(int x, int y, int z)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(Config.RotPitting), 0, 100);
            if (pct <= 0) return false;

            int h = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
            return ((h & 0x7FFFFFFF) % 100) < pct;
        }

        private static float CornerRadius(Vector3Int o, int len, int hgt, int wid)
        {
            int dx = Math.Max(o.x, len - 1 - o.x);
            int dy = Math.Max(o.y, hgt - 1 - o.y);
            int dz = Math.Max(o.z, wid - 1 - o.z);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
