using UnityEngine;
using UnityEngine.Rendering;

namespace FruitLab
{
    internal enum SweepResult
    {
        Clear,
        Hit,
        Blocked,
    }

    internal static class Props
    {
        private static Shader _cachedShader;

        public static GameObject Spawn(string name, Vector3 scale, Color color)
        {
            var obj = new GameObject(name);
            var mf  = obj.AddComponent<MeshFilter>();
            var mr  = obj.AddComponent<MeshRenderer>();

            mf.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            obj.transform.localScale = scale;

            var shader = BorrowedShader();
            if (shader != null)
            {
                mr.material       = new Material(shader);
                mr.material.color = color;
            }
            mr.shadowCastingMode = ShadowCastingMode.Off;

            obj.AddComponent<BoxCollider>();
            return obj;
        }

        public static void Tint(GameObject obj, Color color)
        {
            if (obj == null) return;
            var mr = obj.GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null) mr.material.color = color;
        }

        public static Shader BorrowedShader()
        {
            if (_cachedShader != null) return _cachedShader;

            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                if (r?.sharedMaterial?.shader != null) { _cachedShader = r.sharedMaterial.shader; break; }

            return _cachedShader;
        }

        // ── Flight path ───────────────────────────────────────────────────────

        public static SweepResult SweepForLimb(GameObject self, Vector3 from, Vector3 to,
                                               float radius, out Collider limb, out Vector3 point)
        {
            limb  = null;
            point = to;

            Vector3 seg  = to - from;
            float   span = seg.magnitude;
            int     steps = span > 0.0001f ? Mathf.Clamp(Mathf.CeilToInt(span / radius), 1, 12) : 0;
            Vector3 dir   = span > 0.0001f ? seg / span : self.transform.forward;

            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = steps > 0 ? from + dir * (span * i / steps) : to;

                var outcome = Probe(self, p, radius, out limb, out point);
                if (outcome != SweepResult.Clear) return outcome;
            }

            return SweepResult.Clear;
        }

        private static SweepResult Probe(GameObject self, Vector3 p, float radius,
                                         out Collider limb, out Vector3 point)
        {
            limb  = null;
            point = p;

            var cols = Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore);
            if (cols == null || cols.Length == 0) return SweepResult.Clear;

            float bestDist = float.MaxValue;
            bool  solid    = false;

            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null || IsOwn(self, col)) continue;

                if (Limbs.Of(col.gameObject) != null && col.attachedRigidbody != null)
                {
                    Vector3 cp = col.ClosestPoint(p);
                    float   d  = Vector3.Distance(cp, p);
                    if (d < bestDist) { bestDist = d; limb = col; point = cp; }
                }
                else solid = true;
            }

            if (limb != null) return SweepResult.Hit;
            return solid ? SweepResult.Blocked : SweepResult.Clear;
        }

        private static bool IsOwn(GameObject self, Collider col) =>
            col.gameObject == self || col.transform.IsChildOf(self.transform);
    }
}
