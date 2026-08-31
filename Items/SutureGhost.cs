using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FruitLab
{
    /// <summary>
    /// The Suture Tool's placement preview — a hologram of the limb you are carrying,
    /// standing where it would end up if you clicked now.
    ///
    /// Its own thing rather than the game's ObjectSpawnHologramService: that one is
    /// built for dropping objects on the ground (it takes a MeshDataHandler, turns only
    /// about Y, and lerps towards its target), where this needs an arbitrary six-degree
    /// pose that tracks the crosshair exactly.
    ///
    /// The hologram is a straight copy of the limb's chunk meshes, so it is the actual
    /// silhouette of the actual limb, holes and missing voxels included — a box would
    /// tell you where the limb goes but not how it sits, which was the whole complaint.
    /// Meshes are shared with the original, never copied: the ghost owns nothing but its
    /// transforms and one material.
    /// </summary>
    internal static class SutureGhost
    {
        /// Above 1 so it blooms — see the VFX notes. That glow is most of what makes it
        /// read as a hologram rather than as a second, confusingly solid limb.
        private static readonly Color Good = new Color(0.30f, 1.70f, 1.40f, 1f);
        private static readonly Color Bad  = new Color(1.70f, 0.45f, 0.35f, 1f);

        private static GameObject _root;
        private static Material   _material;
        private static bool       _bad;

        /// Builds the hologram from an assembly, with every piece placed relative to
        /// <paramref name="frame"/> — the transform the tool moves the assembly by, so
        /// posing the ghost is one SetPositionAndRotation on the root.
        public static void Build(Transform assembly, Transform frame)
        {
            Clear();
            if (assembly == null || frame == null) return;

            var shader = Props.BorrowedShader();
            if (shader == null) return;

            _material = new Material(shader) { color = Good };
            _bad      = false;

            _root = new GameObject("FruitLab_SutureGhost");

            MeshFilter[] parts;
            try { parts = assembly.GetComponentsInChildren<MeshFilter>(false); }
            catch { Clear(); return; }

            if (parts == null) { Clear(); return; }

            foreach (var mf in parts)
            {
                if (mf == null || mf.sharedMesh == null) continue;

                // Only what is actually being drawn. A voxel mesh keeps chunks around
                // that it is not showing, and copying those would put flesh in the
                // hologram that is not on the limb.
                var src = mf.GetComponent<MeshRenderer>();
                if (src == null || !src.enabled) continue;

                var piece = new GameObject(mf.name);
                piece.transform.SetParent(_root.transform, false);

                var t = mf.transform;
                piece.transform.localPosition = frame.InverseTransformPoint(t.position);
                piece.transform.localRotation = Quaternion.Inverse(frame.rotation) * t.rotation;
                piece.transform.localScale    = t.lossyScale;

                piece.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;

                var mr = piece.AddComponent<MeshRenderer>();
                mr.sharedMaterial    = _material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows    = false;
            }

            _root.SetActive(false);
        }

        /// Puts the hologram where the limb would land. <paramref name="valid"/> false
        /// colours it as a refusal rather than hiding it, so a bad spot still tells you
        /// what is wrong with it.
        public static void Show(Vector3 position, Quaternion rotation, bool valid)
        {
            if (_root == null) return;

            if (!_root.activeSelf) _root.SetActive(true);
            _root.transform.SetPositionAndRotation(position, rotation);

            if (valid == !_bad) return;

            _bad = !valid;
            if (_material != null) _material.color = valid ? Good : Bad;
        }

        public static void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        public static void Clear()
        {
            if (_root != null) Object.Destroy(_root);
            if (_material != null) Object.Destroy(_material);

            _root     = null;
            _material = null;
            _bad      = false;
        }
    }
}
