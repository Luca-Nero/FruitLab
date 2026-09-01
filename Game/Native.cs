using Il2Cpp;
using Il2CppEffectors;
using Il2CppEffectors.ReceiveMethods.Index;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using Il2CppVoxelMeshGeneration;
using Unity.Mathematics;
using UnityEngine;

namespace FruitLab
{
    internal static class Native
    {
        // ── LimbEffectorReceiver ──────────────────────────────────────────────
        public static VoxelMesh Mesh(LimbEffectorReceiver ler) => ler.wtb;

        public static void Receive(LimbEffectorReceiver ler, bjd signals) =>
            ler.cyn(new bjb<bit>(signals));

        // ── LimbReferencesPublic (zk) ─────────────────────────────────────────
        public static bam Creature(zk refs) => refs.xju;

        public static zf Shape(zk refs) => refs.xkg;

        // ── IReadonlyLimbShapeDataHandler (zf) ────────────────────────────────
        public static int DisabledVoxelsCount(zf shape) => shape.xjr;

        public static int EnabledVoxelsCount(zf shape) => shape.xjp;

        // ── VoxelMesh re-meshing ──────────────────────────────────────────────

        public static void CheckoutUpdate(VoxelMesh mesh) => mesh.dgx(null);

        public static void CreateMesh(VoxelMesh mesh) => mesh.dgq(mesh.pjw, mesh.pjv, true);

        // ── VoxelTools (ct) ───────────────────────────────────────────────────
        public static Vector3Int PositionToVoxelIndex(VoxelMesh mesh, Vector3 p) =>
            ct.dja(mesh, p);

        public static Vector3 VoxelIndexToWorldPosition(VoxelMesh mesh, Vector3Int i) =>
            ct.djd(mesh, Index(i.x, i.y, i.z));

        // ── IndexEffectorSignal ───────────────────────────────────────────────
        public static int3 Index(int x, int y, int z)
        {
            int3 v = default;
            v.x = x; v.y = y; v.z = z;
            return v;
        }

        public static IndexEffectorSignal Signal(int3 index, float value, InfluenceProcessType mode) =>
            new IndexEffectorSignal(index, value, mode);

        public static void AddSignal(bjd batch, int x, int y, int z, float value,
                                     InfluenceProcessType mode) =>
            batch.jbq(Signal(Index(x, y, z), value, mode));

        // ── LVA parameters ────────────────────────────────────────────────────

        public static bcx LimbEntity(zk refs) => refs.xjw;

        public static bcx.bcr ParametersModule(bcx entity) => entity.sqq;

        public static Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Type, bdb>
            ParameterMap(bcx.bcr module) => module.spv;

        public static bcx.bcs ExternalModule(bcx entity) => entity.sqr;

        public static Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Type, bcx.bct>
            ExternalMap(bcx.bcs module) => module.spw;

        public static bda ExternalParam(bcx.bct info) => info.spz;

        public static float Value(bjq p) => p.tcs;

        public static float Initial(bjq p) => p.tcr;

        public static float Min(bjq p) => p.tcq;
        public static float Max(bjq p) => p.tcu;

        public static void SetValue(bjq p, float value) => p.jdl(value);

        // ── Limb hierarchy ────────────────────────────────────────────────────

        public static Il2CppLVA.Limbs.AbstractLimb Limb(zk refs) => refs.xjw;

        public static vd Node(zk refs) => refs.xkb;

        public static bool HasParentNode(vd node) => node.rvc;

        public static vd ParentNode(vd node) => node.rvb;

        public static bbn HierarchyOps(bam creature)
        {
            var handler = creature.smd;
            return handler != null ? handler.soe : null;
        }

        public static bool TryAddAsNative(bbn ops, Il2CppLVA.Limbs.AbstractLimb limb) =>
            ops.ibx(limb);

        public static void AttachParentNode(vd node, vd parent) => node.ham(parent);

        public static void LaunchLVA(Il2CppLVA.Limbs.AbstractLimb limb, bam creature) =>
            limb.hgj(creature);

        // ── Limb ownership: IParentCoresSetter ────────────────────────────────

        public static bcl CoresSetter(zk refs) => refs.xkc;

        public static void SetCreature(bcl setter, bam creature) => setter.idu(creature);

        public static void InstallToAssignedCreature(bcl setter) => setter.idv();

        public static void RemoveCreature(bcl setter) => setter.idw();

        public static void AssignPuppeteer(bcl setter, qb puppeteer) => setter.idx(puppeteer);

        public static qb CreaturePuppeteer(bam creature) => creature.smc;

        public static bool HasPuppeteer(bam creature) => creature.smb;

        public static void AssignCreaturePuppeteer(bam creature, qb puppeteer) =>
            creature.hza(puppeteer);

        public static void InitializePuppeteer(qb puppeteer, bam creature) =>
            puppeteer.ggs(creature);

        public static Il2CppLVA.Limbs.AbstractLimb NodeLimb(vd node) => node.rus;

        public static bool IsNodeNative(vd node) => node.rva;

        public static qb Puppeteer(zk refs) => refs.xjv;

        public static ug NodeHierarchy(bam creature)
        {
            var handler = creature.smd;
            return handler != null ? handler.sod : null;
        }

        public static bool CanAttachAsChild(ug hierarchy, vd node)  => hierarchy.gwf(node);
        public static void AttachAsChild(ug hierarchy, vd node)     => hierarchy.gwe(node);
        public static bool CanAttachAsParent(ug hierarchy, vd node) => hierarchy.gwh(node);
        public static void AttachAsParent(ug hierarchy, vd node)    => hierarchy.gwg(node);

        public static void AttachThirdparty(ug hierarchy, vd child, vd parent) =>
            hierarchy.gwi(child, parent);

        // ── Limb physics ──────────────────────────────────────────────────────

        public static Il2CppLVA.Limbs.LimbPhysics PhysicsOf(zk refs) => refs.xjx;

        public static Transform Pivot(Il2CppLVA.Limbs.LimbPhysics p) => p.xhl;

        public static ConfigurableJoint Joint(Il2CppLVA.Limbs.LimbPhysics p) => p.m_joint;

        public static Rigidbody Body(Il2CppLVA.Limbs.LimbPhysics p) => p.m_rb;
    }
}
