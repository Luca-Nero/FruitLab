using Il2Cpp;
using Il2CppEffectors;
using Il2CppInterop.Runtime;
using Il2CppLVA.Limbs.Variants;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using Il2CppVoxelMeshGeneration;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    /// <summary>
    /// Shared body plumbing: finding limbs, reading their voxel grids, and pushing
    /// effector signal batches into them. Item-agnostic on purpose — anything that
    /// heals, wounds or measures a creature goes through here rather than reaching
    /// for <see cref="Native"/> itself.
    ///
    /// Every method is defensive: the game's accessors can throw on a limb that is
    /// mid-detach, and no item should have to care.
    /// </summary>
    internal static class Limbs
    {
        // ── Lookup ────────────────────────────────────────────────────────────

        /// The receiver owning <paramref name="obj"/>, or null if it is not a limb.
        public static LimbEffectorReceiver Of(GameObject obj)
        {
            var comp = obj.GetComponentInParent(Il2CppType.Of<LimbEffectorReceiver>());
            return comp != null ? comp.TryCast<LimbEffectorReceiver>() : null;
        }

        /// Every limb on the creature this one belongs to.
        ///
        /// Resolved through the limb's AssignedCreature, not transform.root: the
        /// creature prefab is not always the scene root, and a detached limb's root
        /// is itself. Falls back to the limb prefab so a severed limb still works.
        public static Transform CreatureRootOf(LimbEffectorReceiver ler)
        {
            try
            {
                var refs = ler.m_limbReferences;
                if (refs != null)
                {
                    var creature = Native.Creature(refs);
                    if (creature != null && creature.transform != null) return creature.transform;
                }
            }
            catch { }

            return ler.transform.parent != null ? ler.transform.parent : ler.transform;
        }

        /// The creature this limb belongs to, or null for a detached one.
        public static bam CreatureOf(LimbEffectorReceiver ler)
        {
            try
            {
                var refs = ler.m_limbReferences;
                return refs != null ? Native.Creature(refs) : null;
            }
            catch { return null; }
        }

        public static VoxelMesh MeshOf(LimbEffectorReceiver ler)
        {
            try { return Native.Mesh(ler); } catch { return null; }
        }

        public static zf ShapeOf(LimbEffectorReceiver ler)
        {
            try
            {
                var refs = ler.m_limbReferences;
                return refs != null ? Native.Shape(refs) : null;
            }
            catch { return null; }
        }

        /// Whether this creature still has a head attached.
        ///
        /// `Head` is one of the types that kept its real name in v0.1, and being an
        /// AbstractLimb it is a MonoBehaviour, so this is a plain component lookup on
        /// whatever is left of the body.
        public static bool HasHead(Transform creatureRoot)
        {
            if (creatureRoot == null) return false;
            try { return creatureRoot.GetComponentInChildren<Head>(true) != null; }
            catch { return false; }
        }

        /// A world-space point to measure distance to this limb from.
        public static Vector3 SamplePointOf(LimbEffectorReceiver ler)
        {
            var col = ler.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : ler.transform.position;
        }

        // ── Limb graph ────────────────────────────────────────────────────────

        /// A creature's limbs and how they join to each other.
        ///
        /// Built from the physics joints rather than the transform hierarchy, because
        /// every limb is a flat sibling under the creature root — the transform tree
        /// says nothing about what is attached to what. The joints do, they are what
        /// physically holds the body together, and reading them needs no decoding.
        internal sealed class Graph
        {
            public readonly List<LimbEffectorReceiver> Limbs  = new List<LimbEffectorReceiver>();
            /// Index of the limb this one hangs off, or -1 for the root.
            public readonly List<int>   Parent = new List<int>();
            /// The joint holding this limb to its parent. Kept as the joint rather than
            /// as a world position: a position is only true for the frame it was taken
            /// in, and by the time something walking the body arrives at an arm the
            /// creature has usually fallen over, leaving that point somewhere in the air.
            public readonly List<Joint> Joints = new List<Joint>();

            public int Count => Limbs.Count;

            public int IndexOf(LimbEffectorReceiver ler)
            {
                for (int i = 0; i < Limbs.Count; i++) if (Limbs[i] == ler) return i;
                return -1;
            }

            /// Adds a limb the graph did not know about, hanging off <paramref name="parent"/>.
            ///
            /// For pieces that appear after the graph was built — a severed fragment is
            /// a whole new limb, not the old one moved — so the graph has to be able to
            /// grow. There is no joint to record: the fragment is attached to nothing,
            /// which is the point of it, and Junction falls back to the nearest surface.
            public int Append(LimbEffectorReceiver ler, int parent)
            {
                Limbs.Add(ler);
                Parent.Add(parent >= 0 && parent < Limbs.Count - 1 ? parent : -1);
                Joints.Add(null);
                return Limbs.Count - 1;
            }

            /// Everything directly attached to <paramref name="i"/>, in either direction.
            public void Neighbours(int i, List<int> into)
            {
                into.Clear();
                if (Parent[i] >= 0) into.Add(Parent[i]);
                for (int j = 0; j < Parent.Count; j++) if (Parent[j] == i) into.Add(j);
            }

            /// Where two adjacent limbs meet, evaluated now. Whichever of the pair is the
            /// child owns the joint, so its anchor is the shared point.
            public Vector3 Junction(int a, int b)
            {
                Joint j = Parent[b] == a ? Joints[b]
                        : Parent[a] == b ? Joints[a] : null;

                if (j != null)
                {
                    try { return j.transform.TransformPoint(j.anchor); } catch { }
                }

                // No joint — most likely one of them has come off. Fall back to the point
                // on b nearest a, which is still the side it should be entered from.
                try
                {
                    var col = Limbs[b].GetComponentInChildren<Collider>();
                    if (col != null) return col.ClosestPoint(Limbs[a].transform.position);
                }
                catch { }

                return Limbs[b].transform.position;
            }
        }

        public static Graph BuildGraph(Transform creatureRoot)
        {
            var g = new Graph();
            if (creatureRoot == null) return g;

            var bodyToIndex = new Dictionary<int, int>();
            var joints      = new List<Joint>();

            foreach (var ler in creatureRoot.GetComponentsInChildren<LimbEffectorReceiver>(true))
            {
                if (ler == null) continue;

                Rigidbody rb = null;
                Joint     jt = null;
                try
                {
                    rb = ler.GetComponentInParent<Rigidbody>();
                    jt = ler.GetComponentInParent<Joint>();
                }
                catch { }

                if (rb != null && !bodyToIndex.ContainsKey(rb.GetInstanceID()))
                    bodyToIndex[rb.GetInstanceID()] = g.Limbs.Count;

                g.Limbs.Add(ler);
                g.Parent.Add(-1);
                g.Joints.Add(jt);
                joints.Add(jt);
            }

            for (int i = 0; i < g.Limbs.Count; i++)
            {
                var jt = joints[i];
                if (jt == null) continue;

                try
                {
                    var connected = jt.connectedBody;
                    if (connected == null) continue;
                    if (!bodyToIndex.TryGetValue(connected.GetInstanceID(), out int parent)) continue;
                    if (parent == i) continue;

                    g.Parent[i] = parent;
                }
                catch { }
            }

            return g;
        }

        // ── Voxel grid ────────────────────────────────────────────────────────

        public static bool TryGetGrid(VoxelMesh mesh, out int length, out int height, out int width)
        {
            length = height = width = 0;
            try
            {
                var voxels = mesh.pjw;
                length = voxels.length; height = voxels.height; width = voxels.width;
            }
            catch { return false; }

            return length > 0 && height > 0 && width > 0;
        }

        /// World position to a voxel index clamped inside the grid. Returns false if
        /// the game's conversion throws.
        public static bool TryVoxelIndex(VoxelMesh mesh, Vector3 world,
                                         int length, int height, int width, out Vector3Int index)
        {
            index = default;
            try { index = Native.PositionToVoxelIndex(mesh, world); }
            catch { return false; }

            index.x = Math.Clamp(index.x, 0, length - 1);
            index.y = Math.Clamp(index.y, 0, height - 1);
            index.z = Math.Clamp(index.z, 0, width  - 1);
            return true;
        }

        /// A voxel index back to where it currently sits in the world.
        public static bool TryIndexToWorld(VoxelMesh mesh, Vector3Int index, out Vector3 world)
        {
            world = default;
            if (mesh == null) return false;
            try { world = Native.VoxelIndexToWorldPosition(mesh, index); }
            catch { return false; }
            return true;
        }

        /// How many of this limb's voxels are destroyed, or -1 if unavailable.
        /// The cheapest damage readout the game exposes — see NAMES.md.
        public static int DisabledVoxels(zf shape)
        {
            if (shape == null) return -1;
            try { return Native.DisabledVoxelsCount(shape); } catch { return -1; }
        }

        // ── Voxel painting ────────────────────────────────────────────────────

        /// Recolours one voxel in place, keeping its atlas map. Does nothing to a voxel
        /// that is already gone. Colour is a literal per-voxel Color32 in this game, so
        /// this is a direct write — see the voxel colour notes.
        public static bool Paint(VoxelMesh mesh, VoxelMesh.Voxels voxels,
                                 int x, int y, int z, Color32 colour)
        {
            try
            {
                var v = voxels[x, y, z];
                if (!v.enabled) return false;
                voxels.dfc(x, y, z, new VoxelMesh.Voxel(true, new RGBAtlasColor(colour, v.color.map)));
                return true;
            }
            catch { return false; }
        }

        /// Rebuilds a limb's visible mesh from its voxel array.
        ///
        /// Throws a duplicate-key ArgumentException from VoxelMesh.dhh (Show), which
        /// re-adds chunks to a dictionary that already holds them. The geometry has
        /// updated by the time it throws, so it is swallowed and reported once.
        ///
        /// Repeated calls were once suspected of corrupting the mesh — removing them
        /// changed nothing except losing the colour, so that is not what they do. Still
        /// worth throttling: a rebuild is whole-limb and it provokes the throw each time.
        ///
        /// **There is no substitute for this if you want a colour change to be visible.**
        /// Destroying a voxel does make the game re-mesh that chunk, but it re-meshes from
        /// its own changed-data map — only voxels the game itself altered — so colours
        /// written into the array by anyone else are not picked up.
        private static bool _rebuildReported;

        public static void RebuildMesh(VoxelMesh mesh)
        {
            if (mesh == null) return;

            try
            {
                mesh.dgq(mesh.pjw, mesh.pjv, true);
                if (!_rebuildReported)
                {
                    _rebuildReported = true;
                    MelonLogger.Msg("[FruitLab] mesh rebuild returned cleanly (reported once).");
                }
            }
            catch (Exception e)
            {
                // Reported either way, once, because "did the rebuild run at all" is
                // otherwise indistinguishable from "it ran and changed nothing visible".
                if (_rebuildReported) return;
                _rebuildReported = true;
                MelonLogger.Warning($"[FruitLab] mesh rebuild threw (reported once): {e.Message}");
            }
        }

        // ── Signals ───────────────────────────────────────────────────────────

        /// Caller owns the batch and must dispose it — see the usage note in
        /// <see cref="Send"/>.
        public static bjd NewBatch(int capacity) => new bjd(capacity, false);

        /// Destruction progress is health-like, so a positive value heals and a
        /// negative one wounds. Equate sets the voxel outright; Sum accumulates.
        public static void Add(bjd batch, int x, int y, int z, float value,
                               InfluenceProcessType mode) =>
            Native.AddSignal(batch, x, y, z, value, mode);

        /// Hands the batch to the limb. Runs the game's LVA solve synchronously,
        /// severing and death included.
        ///
        /// Faults are logged and swallowed rather than propagated: a receiver that
        /// throws once must not be retired, or the limb goes permanently numb and
        /// presents as "it worked for a while then stopped".
        public static bool Send(LimbEffectorReceiver ler, bjd batch, string label)
        {
            try
            {
                Native.Receive(ler, batch);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Signal batch failed on {label}: {e.Message}");
                return false;
            }
        }
    }
}
