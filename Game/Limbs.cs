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
    internal static class Limbs
    {
        // ── Lookup ────────────────────────────────────────────────────────────

        public static LimbEffectorReceiver Of(GameObject obj)
        {
            var comp = obj.GetComponentInParent(Il2CppType.Of<LimbEffectorReceiver>());
            return comp != null ? comp.TryCast<LimbEffectorReceiver>() : null;
        }

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

        public static bool HasHead(Transform creatureRoot)
        {
            if (creatureRoot == null) return false;
            try { return creatureRoot.GetComponentInChildren<Head>(true) != null; }
            catch { return false; }
        }

        public static zk RefsOf(LimbEffectorReceiver ler)
        {
            try { return ler.m_limbReferences; } catch { return null; }
        }

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

        public static bbn HierarchyOpsOf(bam creature)
        {
            if (creature == null) return null;
            try { return Native.HierarchyOps(creature); } catch { return null; }
        }

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

        public static Il2CppLVA.Limbs.LimbPhysics PhysicsOf(LimbEffectorReceiver ler)
        {
            var refs = RefsOf(ler);
            if (refs == null) return null;
            try { return Native.PhysicsOf(refs); } catch { return null; }
        }

        public static ConfigurableJoint JointOf(LimbEffectorReceiver ler)
        {
            var physics = PhysicsOf(ler);
            if (physics == null) return null;
            try { return Native.Joint(physics); } catch { return null; }
        }

        public static Rigidbody BodyOf(LimbEffectorReceiver ler)
        {
            var physics = PhysicsOf(ler);
            if (physics != null)
            {
                try { var rb = Native.Body(physics); if (rb != null) return rb; } catch { }
            }

            try { return ler.GetComponentInParent<Rigidbody>(); } catch { return null; }
        }

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

        public static bcl CoresSetterOf(LimbEffectorReceiver ler)
        {
            var refs = RefsOf(ler);
            if (refs == null) return null;
            try { return Native.CoresSetter(refs); } catch { return null; }
        }

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

        public static int CreatureId(bam creature)
        {
            if (creature == null) return 0;
            try { return creature.gameObject.GetInstanceID(); } catch { return 0; }
        }

        public static Vector3 SamplePointOf(LimbEffectorReceiver ler)
        {
            var col = ler.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : ler.transform.position;
        }

        // ── Limb graph ────────────────────────────────────────────────────────

        internal sealed class Graph
        {
            public readonly List<LimbEffectorReceiver> Limbs  = new List<LimbEffectorReceiver>();
            public readonly List<int>   Parent = new List<int>();
            public readonly List<Joint> Joints = new List<Joint>();

            public int Count => Limbs.Count;

            public int IndexOf(LimbEffectorReceiver ler)
            {
                for (int i = 0; i < Limbs.Count; i++) if (Limbs[i] == ler) return i;
                return -1;
            }

            public int Append(LimbEffectorReceiver ler, int parent)
            {
                Limbs.Add(ler);
                Parent.Add(parent >= 0 && parent < Limbs.Count - 1 ? parent : -1);
                Joints.Add(null);
                return Limbs.Count - 1;
            }

            public void Neighbours(int i, List<int> into)
            {
                into.Clear();
                if (Parent[i] >= 0) into.Add(Parent[i]);
                for (int j = 0; j < Parent.Count; j++) if (Parent[j] == i) into.Add(j);
            }

            public Vector3 Junction(int a, int b)
            {
                Joint j = Parent[b] == a ? Joints[b]
                        : Parent[a] == b ? Joints[a] : null;

                if (j != null)
                {
                    try { return j.transform.TransformPoint(j.anchor); } catch { }
                }

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

        public static bool TryIndexToWorld(VoxelMesh mesh, Vector3Int index, out Vector3 world)
        {
            world = default;
            if (mesh == null) return false;
            try { world = Native.VoxelIndexToWorldPosition(mesh, index); }
            catch { return false; }
            return true;
        }

        public static int EnabledVoxels(LimbEffectorReceiver ler)
        {
            var shape = ShapeOf(ler);
            if (shape == null) return -1;
            try { return Native.EnabledVoxelsCount(shape); } catch { return -1; }
        }

        public static int DisabledVoxels(zf shape)
        {
            if (shape == null) return -1;
            try { return Native.DisabledVoxelsCount(shape); } catch { return -1; }
        }

        // ── Voxel painting ────────────────────────────────────────────────────

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

        private static bool _rebuildReported;

        public static void RebuildMesh(VoxelMesh mesh)
        {
            if (mesh == null) return;

            try { Native.CreateMesh(mesh); }
            catch (Exception e)
            {
                if (_rebuildReported) return;
                _rebuildReported = true;
                MelonLogger.Warning($"[FruitLab] mesh rebuild threw (reported once): {e.Message}");
            }
        }

        // ── Signals ───────────────────────────────────────────────────────────

        public static bjd NewBatch(int capacity) => new bjd(capacity, false);

        public static void Add(bjd batch, int x, int y, int z, float value,
                               InfluenceProcessType mode) =>
            Native.AddSignal(batch, x, y, z, value, mode);

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
