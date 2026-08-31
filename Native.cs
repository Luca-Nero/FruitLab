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

        // ── Limb hierarchy ────────────────────────────────────────────────────
        //
        // Every limb sits in the creature's node hierarchy as a LimbNode (v0.1: vd),
        // and attaching or detaching a limb is an operation on that hierarchy rather
        // than on the transforms or the joints. The physics follows from it.

        /// LimbReferencesPublic.Limb, typed as the limb rather than as an LVA entity.
        /// AbstractLimb kept its real name in v0.1 and derives from LVAEntity, so this
        /// is the same object <see cref="LimbEntity"/> hands back.
        public static Il2CppLVA.Limbs.AbstractLimb Limb(zk refs) => refs.xjw;

        /// LimbReferencesPublic.Node — this limb's place in the hierarchy.
        public static vd Node(zk refs) => refs.xkb;

        /// LimbNode.HasParent. False for a severed limb — and also for a creature's
        /// root limb, which never had anything above it, so this alone does not mean
        /// "came off".
        public static bool HasParentNode(vd node) => node.rvc;

        /// LimbNode.Parent
        public static vd ParentNode(vd node) => node.rvb;

        /// AbstractCreature.LimbsHierarchyHandler.Operations —
        /// CreatureHierarchyOperationsModule, which owns both halves of the job: its
        /// TryDetach is what the context menu's Detach Limb runs.
        public static bbn HierarchyOps(bam creature)
        {
            var handler = creature.smd;
            return handler != null ? handler.soe : null;
        }

        /// CreatureHierarchyOperationsModule.TryAddAsNative(AbstractLimb) — the exact
        /// inverse of that detach. "Native" is the operative word: it puts the limb
        /// back where it belongs on *this* creature, deriving the socket from the
        /// creature's own hierarchy, and returns false for a limb that is not one of
        /// this creature's own. That refusal is the correctness check, so there is no
        /// need to work out whether an arm is this body's arm before offering it.
        public static bool TryAddAsNative(bbn ops, Il2CppLVA.Limbs.AbstractLimb limb) =>
            ops.ibx(limb);

        /// LimbNode.AttachParent(LimbNode) — the raw node link, below the protocols.
        /// Kept mapped, unused: it joins two nodes and tells nobody, so the creature
        /// never learns it has a new limb. AttachThirdparty is the same move done
        /// properly.
        public static void AttachParentNode(vd node, vd parent) => node.ham(parent);

        /// AbstractLimb.LaunchLVA(AbstractCreature).
        ///
        /// Mapped, and deliberately unused. It does move ownership, but it is the path
        /// the game takes for a limb that was *never* initialised, and on one whose LVA
        /// is already running it re-enters module initialisation and hits a double-call
        /// guard. See ParentCoresSetter below for what the game does with a limb that
        /// has been severed — which is our case, and which does not throw.
        public static void LaunchLVA(Il2CppLVA.Limbs.AbstractLimb limb, bam creature) =>
            limb.hgj(creature);

        // ── Limb ownership: IParentCoresSetter ────────────────────────────────
        //
        // This is where a limb's creature actually lives, and the decompiled
        // NativeLimbAttachModule.TryAddDetachedLimb is what named it. For a severed
        // limb the game walks the whole detached sub-tree and makes two passes over it:
        //
        //     allNodes.ForEach(n => n.assignedLimb.References.ParentCoresSetter
        //                            .SetCreature(newCreature));      // SetCreatureToNodes
        //     allNodes.ForEach(n => n.assignedLimb.References.ParentCoresSetter
        //                            .InstallToAssignedCreature());   // InstallCreatureToNodes
        //
        // then attaches the root node. No LaunchLVA anywhere on that path — that is
        // reserved for the never-initialised case — so no double-call guard to trip.
        //
        // IParentCoresSetter is an interface, so there are no decoys, and v0.1's four
        // members line up with v0.14's declaration order position for position; two of
        // them are unambiguous on signature alone as well.

        /// LimbReferencesPublic.ParentCoresSetter
        public static bcl CoresSetter(zk refs) => refs.xkc;

        /// IParentCoresSetter.SetCreature(AbstractCreature) — hands the limb over.
        public static void SetCreature(bcl setter, bam creature) => setter.idu(creature);

        /// IParentCoresSetter.InstallToAssignedCreature() — the second pass, run only
        /// once every limb in the group knows who it now belongs to.
        public static void InstallToAssignedCreature(bcl setter) => setter.idv();

        /// IParentCoresSetter.RemoveCreature()
        public static void RemoveCreature(bcl setter) => setter.idw();

        /// IParentCoresSetter.AssignPuppeteer(AbstractPuppeteer).
        ///
        /// **Mapped, and not a setter.** It fans out to a list of subscribers rather
        /// than storing anything, and calling it limb-by-limb throws
        /// NullReferenceException out of one of them (`yd.hlr`, reached through a
        /// List.ForEach inside `bbv.idx`) while leaving the limb's AssignedPuppeteer
        /// still null. Tried on a head sutured to a headless body: fifteen limbs
        /// accepted the call, the pelvis threw, and the head reported no puppeteer
        /// afterwards all the same.
        ///
        /// The puppeteer travels with the head — decapitate a creature and the driver
        /// leaves with the skull, which is why a headless body keeps its blood and its
        /// consciousness and still does nothing. Handing it back is a real problem and
        /// this is not the route; the creature-level AbstractCreature.AssignPuppeteer,
        /// or re-running AbstractPuppeteer.Initialize, is where to look next.
        public static void AssignPuppeteer(bcl setter, qb puppeteer) => setter.idx(puppeteer);

        /// AbstractCreature.AssignedPuppeteer
        public static qb CreaturePuppeteer(bam creature) => creature.smc;

        /// AbstractCreature.HasAssignedPuppeteer — the guard on AssignPuppeteer below.
        public static bool HasPuppeteer(bam creature) => creature.smb;

        /// AbstractCreature.AssignPuppeteer(AbstractPuppeteer).
        ///
        /// The creature-level assignment, and the one the game itself uses: it stores
        /// the puppeteer, tells the internal systems module, sets HasAssignedPuppeteer
        /// and walks every limb. Not to be confused with IParentCoresSetter's
        /// same-named method, which only notifies subscribers and stores nothing.
        ///
        /// **Throws DoubleInitialization if the creature already has one**, so check
        /// HasPuppeteer first. Protected in the game; Il2CppInterop widens it, which is
        /// the only reason this is reachable at all.
        ///
        /// `hza` is the sole protected void(AbstractPuppeteer) inside bam's real run
        /// hya…hzc — the other four with that signature are decoys.
        public static void AssignCreaturePuppeteer(bam creature, qb puppeteer) =>
            creature.hza(puppeteer);

        /// AbstractPuppeteer.Initialize(AbstractCreature).
        ///
        /// **Mapped as a dead end. A puppeteer cannot be moved between bodies.**
        ///
        /// AssignPuppeteer tells a creature who drives it; this is meant to tell the
        /// driver which body it drives, and it refuses:
        ///
        ///     SystemException: Double initialization is a sign that you've taken a
        ///     shit somewhere. Exception sender: HumanoidPuppeteer
        ///
        /// Assigning without re-targeting is worse than not assigning at all: the body
        /// is then driven toward the pose and facing of the creature the rig was built
        /// for, which in game is a torso locked into a cower that snaps back when you
        /// try to turn it. Inert is the better of the two, so nothing calls either.
        ///
        /// The consequence is worth stating plainly: **a decapitated body cannot be
        /// given its mind back.** The puppeteer leaves with the skull and the game
        /// assembles a creature once. Reattaching a head restores ownership, vitals and
        /// physics, and the body stays a body.
        public static void InitializePuppeteer(qb puppeteer, bam creature) =>
            puppeteer.ggs(creature);

        /// LimbNode.assignedLimb
        public static Il2CppLVA.Limbs.AbstractLimb NodeLimb(vd node) => node.rus;

        /// LimbNode.Native — whether the hierarchy counts this limb among the
        /// creature's own. Severing costs a limb that standing, which is why
        /// TryAddAsNative will not take a severed limb back.
        public static bool IsNodeNative(vd node) => node.rva;

        /// LimbReferencesPublic.AssignedPuppeteer — null on a limb nothing is
        /// animating, which is the difference between a limb that hangs and one the
        /// body moves.
        public static qb Puppeteer(zk refs) => refs.xjv;

        /// AbstractCreature.LimbsHierarchyHandler.Hierarchy — the node graph itself,
        /// under the operations module.
        public static ug NodeHierarchy(bam creature)
        {
            var handler = creature.smd;
            return handler != null ? handler.sod : null;
        }

        // The two native attach paths, and their preconditions.
        //
        // Which of gwe/gwg is the child one and which is the parent one is derived from
        // v0.14's declaration order alone — v0.1 strips the parameter names, and the
        // xref fingerprints do not carry across for this type (v0.14's
        // DetachNodeFromParent has 25 xrefs where v0.1's counterpart has 2, so the
        // scan results are not comparable here). Do not resolve that uncertainty by
        // guessing: **ask the predicate and call its neighbour.**
        //
        // That is safe whichever way round the labels are. gwe/gwf are adjacent in the
        // sequence and so are gwg/gwh, so each attach sits next to its own
        // precondition; the pairing holds even if "child" and "parent" are swapped.
        // Asking costs nothing, since both predicates are read-only.

        public static bool CanAttachAsChild(ug hierarchy, vd node)  => hierarchy.gwf(node);
        public static void AttachAsChild(ug hierarchy, vd node)     => hierarchy.gwe(node);
        public static bool CanAttachAsParent(ug hierarchy, vd node) => hierarchy.gwh(node);
        public static void AttachAsParent(ug hierarchy, vd node)    => hierarchy.gwg(node);

        /// CreatureNodesHierarchy.AttachNodeAsThirdparty(attachedChildRoot, parent).
        ///
        /// **The one that makes a foreign limb part of a body.** The game supports
        /// grafting limbs that are not a creature's own as a first-class thing — there
        /// is a ThirdpartyNodeAttachProtocol beside the two native ones, and the attach
        /// data carries a ThirdpartyRadiusIndexesDescription of its own — so this is a
        /// road the game already paved, not a hole we are climbing through.
        ///
        /// Unlike TryAddAsNative it asks nothing about whose limb it was and takes an
        /// explicit parent, so a hand goes on a shoulder if that is what you want. And
        /// unlike AttachParentNode it runs the attach protocol, which is what notifies
        /// NativeLimbListenersHandler — the chain the puppeteer's own listener hangs
        /// off, and therefore the only way a sutured limb gets animated rather than
        /// merely held on.
        ///
        /// Argument order is (child, parent); v0.1 stripped the parameter names, so
        /// that is read off v0.14's declaration, where the run gwd…gwk lines up
        /// signature for signature.
        public static void AttachThirdparty(ug hierarchy, vd child, vd parent) =>
            hierarchy.gwi(child, parent);

        // ── Limb physics ──────────────────────────────────────────────────────
        //
        // LimbPhysics is one of the types the obfuscator left entirely alone: the run
        // hjr…hks is unbroken, with no decoys, and m_joint and m_rb kept their real
        // names. So the joint holding a limb on is a plain Unity ConfigurableJoint,
        // reachable and configurable without going through anything obfuscated.

        /// LimbReferencesPublic.Physics
        public static Il2CppLVA.Limbs.LimbPhysics PhysicsOf(zk refs) => refs.xjx;

        /// LimbPhysics.Pivot — the transform the limb hangs from, which is where its
        /// joint anchor sits.
        public static Transform Pivot(Il2CppLVA.Limbs.LimbPhysics p) => p.xhl;

        /// LimbPhysics.m_joint — the ConfigurableJoint holding this limb to its
        /// parent. Null on a limb that was never jointed.
        public static ConfigurableJoint Joint(Il2CppLVA.Limbs.LimbPhysics p) => p.m_joint;

        /// LimbPhysics.m_rb
        public static Rigidbody Body(Il2CppLVA.Limbs.LimbPhysics p) => p.m_rb;
    }
}
