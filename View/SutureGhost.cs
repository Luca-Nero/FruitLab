using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FruitLab
{
    internal static class SutureGhost
    {
        // ── Look ──────────────────────────────────────────────────────────────

        private static readonly Color Good = new Color(0.36f, 1.00f, 0.90f, 1f);
        private static readonly Color Bad  = new Color(1.00f, 0.42f, 0.36f, 1f);

        private const float ScanDensity = 0.2f;
        private const float ScanDrift   = 1f;

        private const float BandAlpha = 0.55f;
        private const float BaseAlpha = 0.15f;

        private static GameObject _root;
        private static Material   _material;
        private static Texture2D  _scan;
        private static bool       _bad;

        private static readonly List<Mesh> _meshes = new List<Mesh>();

        public static void Build(Transform assembly, Transform frame)
        {
            Clear();
            if (assembly == null || frame == null) return;

            bool banded = Dress();
            if (_material == null) return;

            _root = new GameObject("FruitLab_SutureGhost");

            MeshFilter[] parts;
            try { parts = assembly.GetComponentsInChildren<MeshFilter>(false); }
            catch { Clear(); return; }

            if (parts == null) { Clear(); return; }

            foreach (var mf in parts)
            {
                if (mf == null || mf.sharedMesh == null) continue;

                var src = mf.GetComponent<MeshRenderer>();
                if (src == null || !src.enabled) continue;

                var piece = new GameObject(mf.name);
                piece.transform.SetParent(_root.transform, false);

                var t = mf.transform;
                piece.transform.localPosition = frame.InverseTransformPoint(t.position);
                piece.transform.localRotation = Quaternion.Inverse(frame.rotation) * t.rotation;
                piece.transform.localScale    = t.lossyScale;

                var mesh = banded ? Band(mf.sharedMesh, piece.transform.localPosition.y)
                                  : mf.sharedMesh;

                piece.AddComponent<MeshFilter>().sharedMesh = mesh;

                var mr = piece.AddComponent<MeshRenderer>();
                mr.sharedMaterial    = _material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows    = false;
            }

            _root.SetActive(false);
        }

        private static bool Dress()
        {
            var sprite = Shader.Find("Sprites/Default");

            if (sprite != null)
            {
                _scan = ScanTexture();

                _material = new Material(sprite)
                {
                    mainTexture = _scan,
                    color       = Good,
                };

                return true;
            }

            var borrowed = Props.BorrowedShader();
            if (borrowed == null) return false;

            _material = new Material(borrowed) { color = Good * 0.6f };
            return false;
        }

        private const int ScanHeight = 24;

        private static Texture2D ScanTexture()
        {
            var tex = new Texture2D(1, ScanHeight, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            Paint(tex, 0f);
            return tex;
        }

        private static float _phase;

        private static void Drift()
        {
            if (_scan == null) return;

            _phase = Mathf.Repeat(_phase + Time.deltaTime * ScanDrift, 1f);
            Paint(_scan, _phase);
        }

        private static void Paint(Texture2D tex, float phase)
        {
            int shift = Mathf.RoundToInt(phase * ScanHeight);

            for (int y = 0; y < ScanHeight; y++)
            {
                int p = (y + shift) % ScanHeight;

                int   d = Mathf.Min(p, ScanHeight - p);
                float a = d <= 1 ? BandAlpha
                        : d == 2 ? Mathf.Lerp(BaseAlpha, BandAlpha, 0.5f)
                        : BaseAlpha;

                tex.SetPixel(0, y, new Color(a, a, a, a));
            }

            tex.Apply();
        }

        private static Mesh Band(Mesh src, float offset)
        {
            try
            {
                var verts = src.vertices;
                var uvs   = new Il2CppStructArray<Vector2>(verts.Length);

                for (int i = 0; i < verts.Length; i++)
                    uvs[i] = new Vector2(0.5f, (offset + verts[i].y) * ScanDensity);

                var copy = new Mesh
                {
                    vertices  = verts,
                    triangles = src.triangles,
                    uv        = uvs,
                };

                copy.RecalculateBounds();

                _meshes.Add(copy);
                return copy;
            }
            catch
            {
                return src;
            }
        }

        public static void Show(Vector3 position, Quaternion rotation, bool valid)
        {
            if (_root == null) return;

            if (!_root.activeSelf) _root.SetActive(true);
            _root.transform.SetPositionAndRotation(position, rotation);

            Drift();

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

            foreach (var mesh in _meshes)
                if (mesh != null) Object.Destroy(mesh);
            _meshes.Clear();

            if (_material != null) Object.Destroy(_material);
            if (_scan != null)     Object.Destroy(_scan);

            _root     = null;
            _material = null;
            _scan     = null;
            _bad      = false;
        }
    }
}
