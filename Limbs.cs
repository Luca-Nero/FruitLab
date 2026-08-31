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

        /// This limb's references block, or null if it has none.
        public static zk RefsOf(LimbEffectorReceiver ler)
        {
            try { return ler.m_limbReferences; } catch { return null; }
        }

        /// Whether nothing is holding this limb up any more.
        ///
        /// Reads the node hierarchy, not the joints: a limb that has come off gets a
        /// fresh creature of its own, so its joint and its transform parent both look
        /// perfectly reasonable. Note that a creature's *root* limb also has no parent,
        /// so this is "unheld", not "severed" — callers that care about the difference
        /// check the creature as well.
        public static bool IsUnheld(LimbEffectorReceiver ler)
        {
            var refs = RefsOf(ler);
            if (refs == null) return false;

            try
            {
                var node = Native.Node(refs);
                return node != null && !Native.HasParentNode(node);
            }
            catch { return false; }
        }

        /// The hierarchy operations module for the creature this limb belongs to.
        public static bbn HierarchyOpsOf(bam creature)
        {
            if (creature == null) return null;
            try { return Native.HierarchyOps(creature); } catch { return null; }
        }

        /// Puts a limb back on a creature, through the same module the game's own
        /// Detach Limb runs in reverse.
        ///
        /// Returns false when the limb is not native to that creature, which is the
        /// game's own answer rather than a guess of ours, so it is safe to offer it
        /// anything and let it refuse.
        public static bool Reattach(bbn ops, LimbEffectorReceiver ler)
        {
            if (ops == null) return false;

            var refs = RefsOf(ler);
            if (refs == null) return false;

            try
            {
                var limb = Native.Limb(refs);
                if (limb == null) return false;
                return Native.TryAddAsNative(ops, limb);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Reattach failed: {e.Message}");
                return false;
            }
        }

        /// This limb's physics block, or null.
        public static Il2CppLVA.Limbs.LimbPhysics PhysicsOf(LimbEffectorReceiver ler)
        {
            var refs = RefsOf(ler);
            if (refs == null) return null;
            try { return Native.PhysicsOf(refs); } catch { return null; }
        }

        /// The joint holding this limb on, or null.
        public static ConfigurableJoint JointOf(LimbEffectorReceiver ler)
        {
            var physics = PhysicsOf(ler);
            if (physics == null) return null;
            try { return Native.Joint(physics); } catch { return null; }
        }

        /// The rigidbody this limb moves with.
        public static Rigidbody BodyOf(LimbEffectorReceiver ler)
        {
            var physics = PhysicsOf(ler);
            if (physics != null)
            {
                try { var rb = Native.Body(physics); if (rb != null) return rb; } catch { }
            }

            try { return ler.GetComponentInParent<Rigidbody>(); } catch { return null; }
        }

        /// Where this limb attaches — its joint anchor if it has a joint, otherwise
        /// the pivot it hangs from, otherwise the middle of it.
        public static Vector3 AnchorOf(LimbEffectorReceiver ler)
        {
            var joint = JointOf(ler);
            if (joint != null)
            {
                try { return joint.transform.TransformPoint(joint.anchor); } catch { }
            }

            var physics = PhysicsOf(ler);
            if (physics != null)
            {
                try
                {
                    var pivot = Native.Pivot(physics);
                    if (pivot != null) return pivot.position;
                }
                catch { }
            }

            return SamplePointOf(ler);
        }

        /// Makes one limb part of another body, through the game's own third-party
        /// attach protocol.
        ///
        /// Runs on the *parent's* hierarchy, because that is the body gaining a limb.
        /// The protocol is what notifies the creature's limb listeners, so this is the
        /// difference between a limb that is merely held on and one the body knows
        /// about — see Native.AttachThirdparty.
        public static bool GraftNode(LimbEffectorReceiver child, LimbEffectorReceiver parent)
        {
            var childRefs  = RefsOf(child);
            var parentRefs = RefsOf(parent);
            if (childRefs == null || parentRefs == null) return false;

            try
            {
                var childNode  = Native.Node(childRefs);
                var parentNode = Native.Node(parentRefs);
                if (childNode == null || parentNode == null) return false;

                var creature  = Native.Creature(parentRefs);
                var hierarchy = creature != null ? Native.NodeHierarchy(creature) : null;
                if (hierarchy == null)
                {
                    MelonLogger.Warning("[FruitLab] That body has no node hierarchy to graft into.");
                    return false;
                }

                Native.AttachThirdparty(hierarchy, childNode, parentNode);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Node graft failed: {e.Message}");
                return false;
            }
        }

        /// Puts a limb back in its own slot, if the body will still have it.
        ///
        /// Worth trying before anything else, and worth *not* skipping: a severed limb
        /// keeps its native standing (the node still reports Native), and the
        /// third-party graft takes that away — so grafting a body's own arm as a
        /// foreign one quietly demotes it. Only a limb in its native slot carries a
        /// node tag, and the puppeteer addresses limbs by tag, so this is also the only
        /// route by which a reattached limb can end up animated.
        ///
        /// The hierarchy decides where it goes, not the caller, so a limb put back this
        /// way lands in its real socket rather than where the click was.
        ///
        /// Asked of the **body's** hierarchy, never the limb's own. Severing gives a
        /// limb a fresh creature of its own, built from the same prefab and therefore
        /// carrying the same name — so a severed arm and the body it came off both read
        /// as "HumanCreaturePrefab(Clone)" while being different objects entirely.
        /// Reading the hierarchy off the child asks a one-limb creature whether that
        /// limb can attach to itself, and the answer is a perfectly truthful no.
        public static bool GraftNative(LimbEffectorReceiver child, LimbEffectorReceiver parent)
        {
            var refs       = RefsOf(child);
            var parentRefs = RefsOf(parent);
            if (refs == null || parentRefs == null) return false;

            try
            {
                var node = Native.Node(refs);
                if (node == null || !Native.IsNodeNative(node)) return false;

                var creature  = Native.Creature(parentRefs);
                var hierarchy = creature != null ? Native.NodeHierarchy(creature) : null;
                if (hierarchy == null) return false;

                var ops = HierarchyOpsOf(creature);

                // Ask the module first. TryAddAsNative is the whole pipeline — slot,
                // creature, listeners — where the hierarchy calls below only move the
                // node. It has refused before, but it was being asked about a limb
                // whose node was not yet anywhere near this body.
                if (ops != null && Reattach(ops, child))
                {
                    Diag.Log("wiring", $"{Diag.Name(child)} was adopted outright");
                    return true;
                }

                bool asChild  = Native.CanAttachAsChild(hierarchy, node);
                bool asParent = Native.CanAttachAsParent(hierarchy, node);

                Diag.Log("wiring",
                    $"native slot test for {Diag.Name(child)}: " +
                    $"as child {asChild}, as parent {asParent}");

                if (asChild)       Native.AttachAsChild(hierarchy, node);
                else if (asParent) Native.AttachAsParent(hierarchy, node);
                else               return false;

                // And again, now that the node is where it belongs. Putting a limb in
                // the right slot is not the same as the body owning it: the node moves,
                // the LVA membership does not, and a limb that is still its own
                // creature keeps its own blood and its own muscles and hangs there.
                // This is the call that would hand it over.
                if (ops != null && Reattach(ops, child))
                    Diag.Log("wiring", $"{Diag.Name(child)} adopted once the node was in place");
                else
                    Diag.Log("wiring",
                        $"{Diag.Name(child)} is in the right slot but the body will not adopt " +
                        "it — it keeps its own creature, and with it its own vitals");

                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Node graft failed: {e.Message}");
                return false;
            }
        }

        /// Hands a limb over to another body's LVA, by relaunching it under that
        /// creature.
        ///
        /// **Experimental, and off by default.** A limb can sit in the right slot of the
        /// right hierarchy and still belong to a creature of its own — severing gives it
        /// one, and neither the node attach nor TryAddAsNative takes it away again. That
        /// is why a reattached limb keeps its own blood and its own vitals and nothing
        /// animates it. LaunchLVA is the only public method on a limb that names a
        /// creature, so it is the only lever left; what it does to a limb whose LVA is
        /// already running is genuinely unknown, and re-initialising one could plausibly
        /// undo whatever healing was done to it.
        public static bool Adopt(LimbEffectorReceiver child, bam creature)
        {
            var setter = CoresSetterOf(child);
            if (setter == null || creature == null) return false;

            try { Native.SetCreature(setter, creature); }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Could not hand over {Diag.Name(child)}: {e.Message}");
                return false;
            }

            return true;
        }

        /// Hands a whole severed assembly over to another body — the limb at the seam
        /// and everything still hanging off it.
        ///
        /// Two passes, in this order, because that is what the game does: every limb is
        /// told who owns it now, and only then is any of them installed. Installing as
        /// you go would have the first limb wiring itself into a body whose other new
        /// limbs still believe they belong somewhere else.
        ///
        /// Ownership is per limb, so the whole group has to move. Handing over only the
        /// limb at the seam leaves a forearm and hand belonging to a body that exists
        /// nowhere but in their own references — their own blood, and nothing driving
        /// them.
        public static int AdoptAll(List<LimbEffectorReceiver> assembly, bam creature)
        {
            if (assembly == null || creature == null) return 0;

            int taken = 0;
            foreach (var ler in assembly)
                if (ler != null && Adopt(ler, creature)) taken++;

            if (taken == 0) return 0;

            foreach (var ler in assembly)
            {
                if (ler == null) continue;

                var setter = CoresSetterOf(ler);
                if (setter == null) continue;

                try { Native.InstallToAssignedCreature(setter); }
                catch (Exception e)
                {
                    MelonLogger.Warning(
                        $"[FruitLab] {Diag.Name(ler)} would not install: {e.Message}");
                }
            }

            return taken;
        }

        /// Every limb on whatever body this one belongs to.
        public static void CollectBody(LimbEffectorReceiver ler, List<LimbEffectorReceiver> into)
        {
            into.Clear();
            if (ler == null) return;

            try
            {
                var root = CreatureRootOf(ler);
                if (root == null) { into.Add(ler); return; }

                foreach (var other in root.GetComponentsInChildren<LimbEffectorReceiver>(true))
                    if (other != null && !into.Contains(other)) into.Add(other);
            }
            catch { into.Add(ler); }
        }

        /// The interface that owns this limb's creature, or null.
        public static bcl CoresSetterOf(LimbEffectorReceiver ler)
        {
            var refs = RefsOf(ler);
            if (refs == null) return null;
            try { return Native.CoresSetter(refs); } catch { return null; }
        }

        /// What is animating this limb, if anything.
        public static qb PuppeteerOf(LimbEffectorReceiver ler)
        {
            var refs = RefsOf(ler);
            if (refs == null) return null;
            try { return Native.Puppeteer(refs); } catch { return null; }
        }

        public static qb PuppeteerOfCreature(bam creature)
        {
            if (creature == null) return null;
            try { return Native.CreaturePuppeteer(creature); } catch { return null; }
        }

        /// Every limb whose collider occupies a piece of space.
        ///
        /// The allocating OverlapSphere on purpose: the NonAlloc physics overloads
        /// silently report nothing in this build.
        public static void ScanNearby(Vector3 centre, float radius,
                                      List<LimbEffectorReceiver> into)
        {
            into.Clear();

            Collider[] hits;
            try { hits = Physics.OverlapSphere(centre, radius); }
            catch { return; }
            if (hits == null) return;

            foreach (var col in hits)
            {
                if (col == null) continue;

                LimbEffectorReceiver ler = null;
                try { ler = Of(col.gameObject); } catch { }
                if (ler == null || into.Contains(ler)) continue;

                into.Add(ler);
            }
        }

        /// Identity for a creature, compared through its GameObject: two managed
        /// wrappers around the same native object are not necessarily the same
        /// reference, and AbstractCreature is a MonoBehaviour, so this is exact.
        public static int CreatureId(bam creature)
        {
            if (creature == null) return 0;
            try { return creature.gameObject.GetInstanceID(); } catch { return 0; }
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
