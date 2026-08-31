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

        /// VoxelTools.VoxelIndexToWorldPosition(mesh, index) — the inverse of the
        /// above. There are three (VoxelMesh, int3) overloads; this is the one that
        /// accounts for the mesh's rotation, which matters the moment a body falls
        /// over. ct.djc is the matrix variant and ct.djg ignores rotation entirely.
        public static Vector3 VoxelIndexToWorldPosition(VoxelMesh mesh, Vector3Int i) =>
            ct.djd(mesh, Index(i.x, i.y, i.z));

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

        // ── LVA parameters ────────────────────────────────────────────────────
        //
        // A creature, a limb and an organ are all LVAEntity (bcx), each carrying a
        // set of internal parameters — blood, cognition, muscle force, pain. In v0.1
        // a parameter still *derives from* LimitedValue (bjq), so the value lives on
        // the parameter itself; v0.12+ refactored that to composition
        // (LVAParameter.Inner). Port note: reach for `.Inner` there.

        /// LimbReferencesPublic.Limb — the limb as an LVA entity of its own.
        public static bcx LimbEntity(zk refs) => refs.xjw;

        /// LVAEntity.m_internalParameters — the concrete module, not the public API.
        ///
        /// The public API (LVAEntity.EntityInternalParametersPublic, `xlh`) only
        /// offers generic lookups on an interface, and generic-instance virtual
        /// dispatch through an IL2CPP interface does not resolve here: every call
        /// came back empty against a creature that demonstrably had parameters.
        /// The module underneath holds them in a plain dictionary instead.
        public static bcx.bcr ParametersModule(bcx entity) => entity.sqq;

        /// InternalParametersModule.m_parameters — Dictionary&lt;Type, LVAInternalParameter&gt;.
        /// Walkable: a concrete Dictionary hands out a struct enumerator with real
        /// MoveNext/Current, unlike the interface enumerators that cannot be walked.
        public static Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Type, bdb>
            ParameterMap(bcx.bcr module) => module.spv;

        /// LVAEntity.m_externalParameters — the module holding the graph's *inputs*.
        ///
        /// The distinction matters. Internal parameters are outputs: the solver
        /// recomputes them from the externals, so writing one lasts only until the
        /// next solve, which is why a body snaps back to its old numbers the moment
        /// anything disturbs it. Blood is the clearest case — it is a tank, derived
        /// from nothing, so a restored body that reverts to 44% blood can only be
        /// reading that from an input we never touched.
        public static bcx.bcs ExternalModule(bcx entity) => entity.sqr;

        /// ExternalParametersModule.m_externalParameters — Dictionary&lt;Type, ExternalParameterInfo&gt;.
        public static Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Type, bcx.bct>
            ExternalMap(bcx.bcs module) => module.spw;

        /// ExternalParameterInfo.externalParameter. LVAExternalParameter derives from
        /// the same LimitedValue base as everything else, so it takes the same writes.
        public static bda ExternalParam(bcx.bct info) => info.spz;

        /// LimitedValue.m_value — the live value, read as a field so it costs nothing.
        public static float Value(bjq p) => p.tcs;

        /// LimitedValue.initialValue — the value the LimitedValue was *constructed*
        /// with, NOT the creature's spawn state. Measured in-game it is 0 for most
        /// parameters, with LVA raising them to max during initialisation. Diagnostics
        /// only; restoring to it writes zero into everything and kills the body.
        public static float Initial(bjq p) => p.tcr;

        /// LimitedValue.minValue / MaxValue — diagnostics only.
        public static float Min(bjq p) => p.tcq;
        public static float Max(bjq p) => p.tcu;

        /// LimitedValue.SetValue — fires the change notification the dependency
        /// solver listens on, which is why the solve can push the value straight back.
        public static void SetValue(bjq p, float value) => p.jdl(value);
    }
}
