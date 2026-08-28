using Il2Cpp;
using Il2CppEffectors;
using Il2CppEffectors.ReceiveMethods.Index;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using Il2CppVoxelMeshGeneration;
using Unity.Mathematics;
using UnityEngine;

namespace FruitLab
{
    /// <summary>
    /// Every obfuscated game member FruitLab touches, behind a real name.
    /// The v0.1 demo is obfuscated; v0.12+ are not, and the two line up member
    /// for member — see NAMES.md for how each mapping was established. Porting
    /// to a later game build should only need this file rewritten.
    /// </summary>
    internal static class Native
    {
        // ── LimbEffectorReceiver ──────────────────────────────────────────────
        /// LimbEffectorReceiver.VoxelMesh
        public static VoxelMesh Mesh(LimbEffectorReceiver ler) => ler.wtb;

        /// LimbEffectorReceiver.Receive&lt;Destruction&gt;(IndexEffectorSignalsHandler)
        /// Runs the LVA solve synchronously, severing and death included.
        public static void Receive(LimbEffectorReceiver ler, bjd signals) =>
            ler.cyn(new bjb<bit>(signals));

        // ── LimbReferencesPublic (zk) ─────────────────────────────────────────
        /// LimbReferencesPublic.AssignedCreature — a MonoBehaviour, so it carries
        /// the creature's root transform.
        public static bam Creature(zk refs) => refs.xju;

        /// LimbReferencesPublic.ShapeDataHandler
        public static zf Shape(zk refs) => refs.xkg;

        // ── IReadonlyLimbShapeDataHandler (zf) ────────────────────────────────
        /// IReadonlyLimbShapeDataHandler.DisabledVoxelsCount — how many of this
        /// limb's voxels have been destroyed. Cheap, and the only damage readout
        /// reachable without walking the organ collectors.
        public static int DisabledVoxelsCount(zf shape) => shape.xjr;

        // ── VoxelTools (ct) ───────────────────────────────────────────────────
        /// VoxelTools.PositionToVoxelIndex(mesh, worldPosition) — clamped into
        /// the grid. The unclamped overload (ct.djh) is the wrong one here: it
        /// pins far-away points to a corner instead of the nearest cell.
        public static Vector3Int PositionToVoxelIndex(VoxelMesh mesh, Vector3 p) =>
            ct.dja(mesh, p);

        // ── IndexEffectorSignal ───────────────────────────────────────────────
        /// int3 without the interop constructor call — int3 is a real struct in
        /// the generated bindings, but `new int3(x,y,z)` marshals into IL2CPP.
        public static int3 Index(int x, int y, int z)
        {
            int3 v = default;
            v.x = x; v.y = y; v.z = z;
            return v;
        }

        /// Destruction progress is health-like: FruitTweaks wounds with negative Sum
        /// influence, so a large positive Equate clamps a voxel back to whole.
        public static IndexEffectorSignal Signal(int3 index, float value, InfluenceProcessType mode) =>
            new IndexEffectorSignal(index, value, mode);

        /// Appends one signal to a batch. Kept here with the rest of the interop so
        /// callers never need the game's types in scope.
        public static void AddSignal(bjd batch, int x, int y, int z, float value,
                                     InfluenceProcessType mode) =>
            batch.jbq(Signal(Index(x, y, z), value, mode));
    }
}
