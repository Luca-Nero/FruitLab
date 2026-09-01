using FruitLib;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace FruitLab
{
    internal static class SyringeModel
    {
        private static FruitMeshLibrary _meshes;
        private static bool             _tried;

        private const string PartBarrel = "Body";
        private const string PartCap    = "ConnectorCap";
        private const string PartPlunger = "Plunger";
        private const string PartNeedle = "Needle";

        // ══════════════════════════════════════════════════════════════════════════
        // Spawning
        // ══════════════════════════════════════════════════════════════════════════

        public static GameObject Spawn(string name, Vector3 fallbackScale, Color colour)
        {
            if (!Config.SyringeModel) return Props.Spawn(name, fallbackScale, colour);

            var lib = Library();
            if (lib == null || lib.Count == 0) return Props.Spawn(name, fallbackScale, colour);

            var root = new GameObject(name);
            float scale = Mathf.Max(Config.SyringeScale, 0.01f);

            var bounds = new Bounds();
            bool any = false;

            any |= AddPart(root, lib, PartBarrel,  "Barrel",  ref bounds, any);
            any |= AddPart(root, lib, PartCap,     "Cap",     ref bounds, any);
            any |= AddPart(root, lib, PartPlunger, "Plunger", ref bounds, any);
            any |= AddPart(root, lib, PartNeedle,  "Needle",  ref bounds, any);

            if (!any)
            {
                Object.Destroy(root);
                return Props.Spawn(name, fallbackScale, colour);
            }

            float lead = bounds.max.z;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var t = root.transform.GetChild(i);
                t.localPosition = new Vector3(0f, 0f, -lead);
            }

            root.transform.localScale = Vector3.one * scale;

            var box    = root.AddComponent<BoxCollider>();
            box.center = bounds.center - new Vector3(0f, 0f, lead);
            box.size   = bounds.size;

            Tint(root, colour);

            Plunge(root, 0f);

            return root;
        }

        private static bool AddPart(GameObject root, FruitMeshLibrary lib, string meshName,
                                    string label, ref Bounds bounds, bool haveBounds)
        {
            var mesh = lib.GetMesh(meshName);
            if (mesh == null) return false;

            var go = new GameObject(label);
            go.transform.SetParent(root.transform, false);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            Dress(mr, lib.GetMaterials(meshName), label == "Barrel");

            if (haveBounds) bounds.Encapsulate(mesh.bounds);
            else            bounds = mesh.bounds;

            return true;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Materials
        // ══════════════════════════════════════════════════════════════════════════

        private static void Dress(MeshRenderer mr, FruitMaterialGroup[] groups, bool clear)
        {
            var shader = Solid();
            if (shader == null) return;

            if (groups == null || groups.Length == 0)
            {
                mr.material = Make(shader, new Color(0.8f, 0.8f, 0.8f, 1f), clear);
                return;
            }

            var mats = new Material[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                var kd = g.Kd ?? new[] { 0.8f, 0.8f, 0.8f };

                float alpha = clear ? Mathf.Clamp01(Config.SyringeGlassAlpha) : g.Alpha;

                mats[i] = Make(shader, new Color(kd[0], kd[1], kd[2], alpha), clear);
            }

            mr.materials = mats;
        }

        private static Material Make(Shader shader, Color colour, bool transparent)
        {
            var mat = new Material(shader) { hideFlags = HideFlags.DontUnloadUnusedAsset };

            if (transparent && colour.a < 0.999f) Glass(mat);

            mat.color = colour;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);

            return mat;
        }

        private static void Glass(Material mat)
        {
            mat.SetFloat("_Surface",   1f);
            mat.SetFloat("_Blend",     0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_ZWrite",    0f);
            mat.SetFloat("_SrcBlend",  (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend",  (float)BlendMode.OneMinusSrcAlpha);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Shader _solid;

        private static Shader Solid()
        {
            if (_solid != null) return _solid;

            _solid = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard")
                  ?? Loaded("Universal Render Pipeline/Lit")
                  ?? Loaded("Lit")
                  ?? Props.BorrowedShader();

            if (_solid == null) MelonLogger.Warning("[FruitLab] No shader for the syringe model.");
            else                Diag.Log("model", $"syringe shader: {_solid.name}");

            return _solid;
        }

        private static Shader Loaded(string name)
        {
            try
            {
                foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>())
                    if (sh != null && sh.name == name) return sh;

                foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>())
                    if (sh != null && sh.name != null &&
                        sh.name.EndsWith(name, System.StringComparison.OrdinalIgnoreCase))
                        return sh;
            }
            catch { }

            return null;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Plunger
        // ══════════════════════════════════════════════════════════════════════════

        public static void Plunge(GameObject root, float t01)
        {
            if (root == null) return;

            var plunger = root.transform.Find("Plunger");
            if (plunger == null) return;

            var barrel = root.transform.Find("Barrel");
            Vector3 home = barrel != null ? barrel.localPosition : Vector3.zero;

            float draw = Config.SyringePlungerDraw * (1f - Mathf.Clamp01(t01));
            plunger.localPosition = home + new Vector3(0f, 0f, draw);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Tinting
        // ══════════════════════════════════════════════════════════════════════════

        public static void Tint(GameObject root, Color colour)
        {
            if (root == null) return;

            var barrel = root.transform.Find("Barrel");
            if (barrel == null) { Props.Tint(root, colour); return; }

            var mr = barrel.GetComponent<MeshRenderer>();
            if (mr == null) return;

            foreach (var mat in mr.materials)
            {
                if (mat == null) continue;

                var c = new Color(colour.r, colour.g, colour.b, mat.color.a);
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Library
        // ══════════════════════════════════════════════════════════════════════════

        private static FruitMeshLibrary Library()
        {
            if (_tried) return _meshes;
            _tried = true;

            try
            {
                _meshes = new FruitMeshLibrary(Assembly.GetExecutingAssembly());
                if (_meshes.Count == 0)
                {
                    MelonLogger.Warning("[FruitLab] No syringe meshes embedded — using the primitive.");
                    _meshes = null;
                }
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Syringe meshes failed to load: {e.Message}");
                _meshes = null;
            }

            return _meshes;
        }

        public static void OnSceneReload() { }
    }
}
