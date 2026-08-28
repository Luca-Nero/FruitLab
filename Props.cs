using UnityEngine;
using UnityEngine.Rendering;

namespace FruitLab
{
    internal enum SweepResult
    {
        /// Nothing in the way.
        Clear,
        /// A limb was found; see the out parameters.
        Hit,
        /// World geometry is in the way, so the path stops here.
        Blocked,
    }

    /// <summary>
    /// Shared helpers for physical props an item throws or places: building the
    /// object, tinting it, and testing its flight path against creature limbs.
    /// Item-agnostic — a dart, a tracker or a grenade wants all of this too.
    /// </summary>
    internal static class Props
    {
        private static Shader _cachedShader;

        /// A small unlit-ish box prop with a rigidbody-ready collider. The caller
        /// adds the Rigidbody, so it can set velocity in the same frame.
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

        /// Borrows a shader from any loaded renderer. Resolved on first use and
        /// cached — scanning every object in memory per spawn is expensive, and
        /// warming it at init is too early for the game's own materials to exist.
        public static Shader BorrowedShader()
        {
            if (_cachedShader != null) return _cachedShader;

            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                if (r?.sharedMaterial?.shader != null) { _cachedShader = r.sharedMaterial.shader; break; }

            return _cachedShader;
        }

        // ── Flight path ───────────────────────────────────────────────────────

        /// Walks the segment <paramref name="from"/> → <paramref name="to"/> looking
        /// for a limb to make contact with, ignoring <paramref name="self"/>.
        ///
        /// Marched as a chain of overlap probes rather than one swept cast:
        /// Physics.SphereCastAll is stripped from this build and throws
        /// NotSupportedException, while OverlapSphere is known good. Probing also
        /// hands back the full collider list at each point, which is how the prop's
        /// own collider gets filtered out — a single-hit cast keeps returning the
        /// caster itself, which is what made sticking unreliable to begin with.
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

        /// Looks at everything overlapping one point. A limb wins over world geometry
        /// at the same point, which is the alignment tolerance: a fat probe grazes the
        /// floor under a prone body well before it reaches the body, and treating that
        /// graze as a wall would refuse every throw at anything lying down.
        private static SweepResult Probe(GameObject self, Vector3 p, float radius,
                                         out Collider limb, out Vector3 point)
        {
            limb  = null;
            point = p;

            // Allocating overload on purpose: OverlapSphereNonAlloc silently returns
            // nothing against this game's IL2CPP bindings.
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
