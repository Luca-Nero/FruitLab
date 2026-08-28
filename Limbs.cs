using Il2Cpp;
using Il2CppEffectors;
using Il2CppInterop.Runtime;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using Il2CppVoxelMeshGeneration;
using MelonLoader;
using System;
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

        /// A world-space point to measure distance to this limb from.
        public static Vector3 SamplePointOf(LimbEffectorReceiver ler)
        {
            var col = ler.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : ler.transform.position;
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

        /// How many of this limb's voxels are destroyed, or -1 if unavailable.
        /// The cheapest damage readout the game exposes — see NAMES.md.
        public static int DisabledVoxels(zf shape)
        {
            if (shape == null) return -1;
            try { return Native.DisabledVoxelsCount(shape); } catch { return -1; }
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
