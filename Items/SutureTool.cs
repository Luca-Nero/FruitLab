using FruitLib;
using HarmonyLib;
using Il2CppEffectors;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Suture Tool — puts limbs on bodies. Pick one up, carry it, click where it goes.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class SutureTool
    {
        public const string ItemId      = "FruitLab:Suture";
        public const string DisplayName = "Suture Tool";

        public static readonly Color IconColor = new Color(0.55f, 0.80f, 0.74f, 1f);

        private static bool _equipped;

        private static LimbEffectorReceiver _held;
        private static string     _heldName;

        private static Vector3 _anchorLocal;
        private static Vector3 _axisLocal;

        private static Vector3 _boxCentre;
        private static Vector3 _boxExtents = Vector3.one * 0.05f;

        private static int   _face;
        private static float _spin;

        private static bool       _placing;
        private static float      _glide;
        private static Vector3    _fromAnchor, _toSeam;
        private static Quaternion _delta;
        private static LimbEffectorReceiver _onto;
        private static Rigidbody  _ontoBody;

        private sealed class Suspended
        {
            public Rigidbody  Rb;
            public bool       WasKinematic;

            public Vector3    FromPos;
            public Quaternion FromRot;
        }

        private static readonly HashSet<int> _settling = new HashSet<int>();
        private static float _settleUntil;

        private static readonly List<Suspended> _carried = new List<Suspended>();
        private static readonly List<Collider>  _ghosted = new List<Collider>();
        private static readonly List<Rigidbody> _scratch = new List<Rigidbody>();
        private static readonly List<LimbEffectorReceiver> _watched =
            new List<LimbEffectorReceiver>();
        private static readonly List<LimbEffectorReceiver> _assembly =
            new List<LimbEffectorReceiver>();
        private static readonly List<LimbEffectorReceiver> _bodyLimbs =
            new List<LimbEffectorReceiver>();

        // ══════════════════════════════════════════════════════════════════════════
        // Toolbar item
        // ══════════════════════════════════════════════════════════════════════════

        public static void Equip() => _equipped = true;

        public static void Unequip()
        {
            _equipped = false;
            Release();
        }

        public static bool Carrying() => _equipped && _placing;

        public static void Click()
        {
            if (_placing) return;

            if (_held == null) Pick();
            else               Place();
        }

        public static void OnSceneReload()
        {
            _equipped = false;
            Forget();
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loop
        // ══════════════════════════════════════════════════════════════════════════

        public static void OnUpdate()
        {
            if (!_equipped || FruitMenu.BlocksGameplayInput) return;

            if (_held != null)
            {
                bool gone;
                try { gone = _held.transform == null; } catch { gone = true; }
                if (gone) { Forget(); return; }

                if (!_placing && Input.GetMouseButtonDown(1))
                {
                    MelonLogger.Msg($"[FruitLab] Put {_heldName} back down.");
                    Release();
                    return;
                }

                if (_placing) Glide(Time.deltaTime);
                else            { Turn(); Preview(); }
            }
        }

        public static void OnGUI()
        {
            if (!_equipped || _held == null) return;

            var style = FruitLabHud.Text(13);
            var prev  = GUI.color;

            string line = _placing
                ? $"Suturing {_heldName}…"
                : $"{_heldName} — joining by its {FaceName(_face)}, spun {_spin:0}°   " +
                  $"[{Config.SutureKeyFacePrev}/{Config.SutureKeyFaceNext} face, " +
                  $"{Config.SutureKeySpinLeft}/{Config.SutureKeySpinRight} spin, " +
                  "click to sew, right click to drop]";

            var size = style.CalcSize(new GUIContent(line));
            var rect = new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.72f,
                                size.x, size.y);

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), line, style);

            GUI.color = FruitLabHud.Held;
            GUI.Label(rect, line, style);
            GUI.color = prev;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Pick up and carry
        // ══════════════════════════════════════════════════════════════════════════

        private static void Pick()
        {
            if (!Aim(out RaycastHit hit)) return;

            var ler = Limbs.Of(hit.collider.gameObject);
            if (ler == null) return;

            if (!Limbs.IsUnheld(ler))
            {
                MelonLogger.Msg("[FruitLab] That limb is still attached — cut it off first.");
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            _held     = ler;
            _heldName = NameOf(ler);

            var body = Limbs.BodyOf(ler);
            Quaternion held = body != null ? body.rotation : ler.transform.rotation;
            Vector3    at   = body != null ? body.position : ler.transform.position;

            Vector3 anchor = Limbs.AnchorOf(ler);
            Vector3 axis   = Extent(ler, anchor) - anchor;

            _anchorLocal = Quaternion.Inverse(held) * (anchor - at);
            _axisLocal   = axis.sqrMagnitude > 1e-6f
                         ? Quaternion.Inverse(held) * axis.normalized
                         : Vector3.zero;

            MeasureBox(ler, at, held);

            Suspend(ler);

            _face = 3;
            _spin = 0f;

            if (Config.SutureGhost && body != null)
            {
                Transform assembly = null;
                try { assembly = Limbs.CreatureRootOf(ler); } catch { }
                SutureGhost.Build(assembly != null ? assembly : ler.transform, body.transform);
            }

            MelonLogger.Msg($"[FruitLab] Picked up {_heldName}.");

            if (Diag.On)
            {
                var joint = Limbs.JointOf(ler);
                Transform picked = null;
                try { picked = Limbs.CreatureRootOf(ler); } catch { }

                Diag.Log("suture",
                    $"picked up {_heldName} from {(picked != null ? picked.name : "<no root>")}: " +
                    $"{_assembly.Count} limb(s), {_carried.Count} rigidbody(s), " +
                    $"{_ghosted.Count} collider(s), " +
                    $"{(joint != null ? "has its original joint" : "NO joint — one will be built")}, " +
                    $"axis {(_axisLocal == Vector3.zero ? "unknown" : _axisLocal.ToString("0.00"))}");

                foreach (var part in _assembly) Diag.Wiring("  carrying", part);
            }
        }

        private static void Turn()
        {
            float step = Mathf.Max(Config.SutureTurnStep, 1f);

            if      (Input.GetKeyDown(Config.SutureKeyFacePrev)) _face = (_face + 5) % 6;
            else if (Input.GetKeyDown(Config.SutureKeyFaceNext)) _face = (_face + 1) % 6;
            else if (Input.GetKeyDown(Config.SutureKeySpinLeft))  _spin = Mathf.Repeat(_spin - step, 360f);
            else if (Input.GetKeyDown(Config.SutureKeySpinRight)) _spin = Mathf.Repeat(_spin + step, 360f);
        }

        private static Vector3 FaceNormal(int face)
        {
            switch (face)
            {
                case 0:  return Vector3.right;
                case 1:  return Vector3.left;
                case 2:  return Vector3.up;
                case 3:  return Vector3.down;
                case 4:  return Vector3.forward;
                default: return Vector3.back;
            }
        }

        private static string FaceName(int face)
        {
            switch (face)
            {
                case 0:  return "right";
                case 1:  return "left";
                case 2:  return "top";
                case 3:  return "bottom";
                case 4:  return "front";
                default: return "back";
            }
        }

        private static void Preview()
        {
            var body = Limbs.BodyOf(_held);
            if (body == null) return;

            if (!Config.SutureGhost) return;

            if (!Aim(out RaycastHit hit)) { SutureGhost.Hide(); return; }

            var parent = Limbs.Of(hit.collider.gameObject);
            if (parent == null) { SutureGhost.Hide(); return; }

            Solve(hit, body, out _, out _, out _, out Vector3 pos, out Quaternion rot);
            SutureGhost.Show(pos, rot, !Carrying(parent) && Limbs.BodyOf(parent) != null);
        }

        private static void Solve(RaycastHit hit, Rigidbody childBody,
                                  out Vector3 seam, out Quaternion delta, out Vector3 anchor,
                                  out Vector3 position, out Quaternion rotation)
        {
            Vector3 normal = hit.normal.sqrMagnitude > 1e-6f
                           ? hit.normal.normalized : Vector3.up;
            seam = hit.point + normal * Config.SutureSeamOffset;

            Vector3 outward = FaceNormal(_face);
            Vector3 face    = _boxCentre + outward * _boxExtents[_face / 2];

            rotation = Quaternion.AngleAxis(_spin, -normal)
                     * Quaternion.FromToRotation(outward, -normal);

            anchor = childBody.position + childBody.rotation * face;
            delta  = rotation * Quaternion.Inverse(childBody.rotation);

            position = seam + delta * (childBody.position - anchor);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Sew on
        // ══════════════════════════════════════════════════════════════════════════

        private static void Place()
        {
            if (!Aim(out RaycastHit hit)) return;

            var parent = Limbs.Of(hit.collider.gameObject);
            if (parent == null) return;

            if (Carrying(parent))
            {
                MelonLogger.Msg("[FruitLab] That is part of the limb you are holding.");
                return;
            }

            var parentBody = Limbs.BodyOf(parent);
            if (parentBody == null)
            {
                MelonLogger.Warning("[FruitLab] That limb has no rigidbody to join to.");
                return;
            }

            var childBody = Limbs.BodyOf(_held);
            if (childBody == null)
            {
                MelonLogger.Warning("[FruitLab] That limb has no rigidbody to join.");
                return;
            }

            Solve(hit, childBody, out Vector3 seam, out Quaternion delta,
                  out Vector3 anchor, out _, out _);

            if (Diag.On)
            {
                _watched.Clear();
                Limbs.CollectBody(_held, _bodyLimbs);
                _watched.AddRange(_bodyLimbs);
                Limbs.CollectBody(parent, _bodyLimbs);
                foreach (var ler in _bodyLimbs)
                    if (!_watched.Contains(ler)) _watched.Add(ler);
                Diag.WatchVoxels("suture", _watched, Mathf.Max(Config.DiagWindow, 0.5f));

                Diag.Survey("survey", _watched);

                Diag.Log("suture",
                    $"sewing {_heldName} onto {NameOf(parent)} at {seam:0.00}, " +
                    $"turn {Quaternion.Angle(Quaternion.identity, delta):0}deg");

                Diag.Log("suture",
                    $"settings: seamOffset {Config.SutureSeamOffset:0.000}m, " +
                    $"seamCollision {Config.SutureSeamCollision}, " +
                    $"settle {Config.SutureSettle:0.00}s, " +
                    $"breakForce {(Config.SutureBreakForce > 0f ? Config.SutureBreakForce.ToString("0") : "none")}, " +
                    $"native {Config.SutureNative}, graft {Config.SutureGraft}, " +
                    $"adopt {Config.SutureAdopt}, align {Config.SutureAlign}");
            }

            Diag.Wiring("before", _held);

            _onto       = parent;
            _ontoBody   = parentBody;
            _toSeam     = seam;
            _fromAnchor = anchor;
            _delta      = delta;
            _glide      = 0f;
            _placing    = true;

            foreach (var sus in _carried)
            {
                if (sus.Rb == null) continue;
                sus.FromPos = sus.Rb.position;
                sus.FromRot = sus.Rb.rotation;
            }

            SutureGhost.Hide();
            Glide(0f);
        }

        private static void Glide(float dt)
        {
            float span = Mathf.Max(Config.SutureGlide, 0.0001f);
            _glide = Mathf.Clamp01(_glide + dt / span);

            float      t       = Mathf.SmoothStep(0f, 1f, _glide);
            Quaternion partial = Quaternion.Slerp(Quaternion.identity, _delta, t);
            Vector3    target  = Vector3.Lerp(_fromAnchor, _toSeam, t);

            foreach (var sus in _carried)
            {
                var rb = sus.Rb;
                if (rb == null) continue;

                try
                {
                    var tr = rb.transform;

                    tr.position = target + partial * (sus.FromPos - _fromAnchor);
                    tr.rotation = partial * sus.FromRot;

                    rb.position = tr.position;
                    rb.rotation = tr.rotation;
                }
                catch { }
            }

            if (_glide < 1f) return;

            Finish();
        }

        private static void Finish()
        {
            _placing = false;

            var parent     = _onto;
            var parentBody = _ontoBody;
            Vector3 seam   = _toSeam;

            if (parent == null || parentBody == null) { Release(); return; }

            Diag.Sample("moved into place");

            string name    = _heldName;
            bool   jointed = Join(_held, parentBody, seam);
            Diag.Sample("joint wired");

            string how = "not grafted";
            bool grafted = false;

            if (Config.SutureNative && Limbs.GraftNative(_held, parent))
            {
                grafted = true;
                how     = "into its own socket";
            }
            else if (Config.SutureGraft && Limbs.GraftNode(_held, parent))
            {
                grafted = true;
                how     = "as a foreign limb";
            }

            Diag.Sample("hierarchy grafted");
            Diag.Wiring("after", _held);

            Diag.Vitals("body before", parent);

            if (grafted && Config.SutureAdopt)
            {
                var body = Limbs.CreatureOf(parent);

                var carriedDriver = Limbs.PuppeteerOf(_held);

                int taken = Limbs.AdoptAll(_assembly, body);

                Diag.Log("wiring",
                    $"{taken} of {_assembly.Count} limb(s) handed over to {Diag.Body(body)}");

                if (carriedDriver != null && Limbs.PuppeteerOfCreature(body) == null)
                {
                    MelonLogger.Msg(
                        $"[FruitLab] {name} carries the mind and {NameOf(parent)} has none, " +
                        "so the result will not move. Carry the body and sew it onto the head " +
                        "instead — the body joins the head's creature, and the whole thing wakes.");
                }

                foreach (var ler in _assembly) Diag.Wiring("after adoption", ler);
                Diag.Vitals("body after", parent);
            }

            Settle();

            Solidify();
            Diag.Sample("colliders back on");

            if (!Config.SutureSeamCollision) Unclash(_held, parent);

            Reanimate();
            Diag.Sample("weight back on");

            if (!jointed)
            {
                MelonLogger.Warning($"[FruitLab] {name} was placed but could not be joined.");
                return;
            }

            MelonLogger.Msg(grafted
                ? $"[FruitLab] Sutured {name} onto {NameOf(parent)}, {how}."
                : $"[FruitLab] Sutured {name} onto {NameOf(parent)} — physically only, " +
                  "the hierarchy would not take it.");
        }

        private static bool Join(LimbEffectorReceiver child, Rigidbody parentBody, Vector3 seam)
        {
            try
            {
                var joint = Limbs.JointOf(child);
                bool made = false;

                if (joint == null)
                {
                    var rb = Limbs.BodyOf(child);
                    if (rb == null) return false;

                    joint = rb.gameObject.AddComponent<ConfigurableJoint>();
                    made  = true;
                }

                joint.anchor = joint.transform.InverseTransformPoint(seam);

                joint.autoConfigureConnectedAnchor = false;
                joint.connectedBody   = parentBody;
                joint.connectedAnchor = parentBody.transform.InverseTransformPoint(seam);

                joint.enablePreprocessing = false;
                joint.enableCollision     = Config.SutureSeamCollision;

                if (made)
                {
                    joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
                    joint.angularXMotion = joint.angularYMotion = joint.angularZMotion =
                        ConfigurableJointMotion.Free;
                }

                float limit = Config.SutureBreakForce;
                joint.breakForce  = limit > 0f ? limit : Mathf.Infinity;
                joint.breakTorque = limit > 0f ? limit : Mathf.Infinity;

                Diag.Log("suture",
                    $"joint {(made ? "built" : "reused")}: connected to {parentBody.name}, " +
                    $"anchor {joint.anchor:0.000}, connectedAnchor {joint.connectedAnchor:0.000}, " +
                    $"break {joint.breakForce:0}");

                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Joining failed: {e.Message}");
                return false;
            }
        }

        private static void Unclash(LimbEffectorReceiver child, LimbEffectorReceiver parent)
        {
            try
            {
                Collider[] mine = child.GetComponentsInChildren<Collider>(true);
                if (mine == null) return;

                Transform body = null;
                try { body = Limbs.CreatureRootOf(parent); } catch { }

                var theirs = (body != null ? body : parent.transform)
                             .GetComponentsInChildren<Collider>(true);
                if (theirs == null) return;

                int pairs = 0;

                foreach (var a in mine)
                {
                    if (a == null) continue;

                    foreach (var b in theirs)
                    {
                        if (b == null || b == a) continue;
                        try { Physics.IgnoreCollision(a, b, true); pairs++; } catch { }
                    }
                }

                Diag.Log("suture",
                    $"seam collision off for {pairs} pair(s): {Diag.Name(child)} against " +
                    $"{(body != null ? body.name : NameOf(parent))} — the rest of the " +
                    "assembly still collides");
            }
            catch { }
        }

        internal static bool Watching(Rigidbody a, Rigidbody b)
        {
            if (_settling.Count == 0) return false;

            try
            {
                if (a != null && _settling.Contains(a.GetInstanceID())) return true;
                if (b != null && _settling.Contains(b.GetInstanceID())) return true;
            }
            catch { }

            return false;
        }

        private static void Settle()
        {
            _settling.Clear();

            foreach (var s in _carried)
            {
                try { if (s.Rb != null) _settling.Add(s.Rb.GetInstanceID()); } catch { }
            }

            _settleUntil = Time.time + Mathf.Max(Config.SutureSettle, 0f);
        }

        internal static void NoteImpact(string on, string against, float impulse, bool held)
        {
            if (!Diag.On) return;
            Diag.Log("suture",
                $"impact on {on} from {against}, impulse {impulse:0.0}" +
                (held ? " — damage held off" : " — LET THROUGH"));
        }

        internal static bool Settling(Rigidbody a, Rigidbody b)
        {
            if (_settling.Count == 0 || Time.time > _settleUntil) return false;

            try
            {
                if (a != null && _settling.Contains(a.GetInstanceID())) return true;
                if (b != null && _settling.Contains(b.GetInstanceID())) return true;
            }
            catch { }

            return false;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Suspend / release
        // ══════════════════════════════════════════════════════════════════════════

        private static void Suspend(LimbEffectorReceiver child)
        {
            _carried.Clear();
            _ghosted.Clear();

            Transform root = null;
            try { root = Limbs.CreatureRootOf(child); } catch { }

            Collect(child, root);

            foreach (var rb in _scratch)
            {
                if (rb == null) continue;

                try
                {
                    _carried.Add(new Suspended { Rb = rb, WasKinematic = rb.isKinematic });
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic     = true;
                }
                catch { }
            }

            try
            {
                var source = root != null ? root : child.transform;

                foreach (var col in source.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null || !col.enabled) continue;

                    col.enabled = false;
                    _ghosted.Add(col);
                }

                _assembly.Clear();
                foreach (var ler in source.GetComponentsInChildren<LimbEffectorReceiver>(true))
                    if (ler != null && !_assembly.Contains(ler)) _assembly.Add(ler);
            }
            catch { }
        }

        private static void Release()
        {
            Solidify();
            Reanimate();
        }

        private static void Solidify()
        {
            foreach (var col in _ghosted)
            {
                try { if (col != null) col.enabled = true; } catch { }
            }
        }

        private static void Reanimate()
        {
            foreach (var s in _carried)
            {
                try
                {
                    if (s.Rb == null) continue;

                    s.Rb.isKinematic     = s.WasKinematic;
                    s.Rb.linearVelocity  = Vector3.zero;
                    s.Rb.angularVelocity = Vector3.zero;
                    if (!s.WasKinematic) s.Rb.WakeUp();
                }
                catch { }
            }

            Forget();
        }

        private static void Forget()
        {
            SutureGhost.Clear();

            _placing  = false;
            _glide    = 0f;
            _onto     = null;
            _ontoBody = null;

            _held     = null;
            _heldName = null;
            _carried.Clear();
            _ghosted.Clear();
            _scratch.Clear();
            _assembly.Clear();
            _bodyLimbs.Clear();
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════════

        private static void Move(Vector3 anchor, Vector3 target, Quaternion turn)
        {
            foreach (var s in _carried)
            {
                var rb = s.Rb;
                if (rb == null) continue;

                try
                {
                    var t = rb.transform;

                    t.position = target + turn * (t.position - anchor);
                    t.rotation = turn * t.rotation;

                    rb.position = t.position;
                    rb.rotation = t.rotation;
                }
                catch { }
            }
        }

        private static bool Aim(out RaycastHit hit)
        {
            hit = default;

            var cam = Camera.main;
            if (cam == null) return false;

            var t = cam.transform;
            return Physics.Raycast(t.position, t.forward, out hit,
                                   Mathf.Max(Config.SutureAimRange, 0.5f),
                                   ~0, QueryTriggerInteraction.Ignore);
        }

        private static void Collect(LimbEffectorReceiver child, Transform root)
        {
            _scratch.Clear();

            if (root != null)
            {
                try
                {
                    foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                        if (rb != null) _scratch.Add(rb);
                }
                catch { }
            }

            if (_scratch.Count > 0) return;

            var own = Limbs.BodyOf(child);
            if (own != null) _scratch.Add(own);
        }

        private static bool Carrying(LimbEffectorReceiver ler)
        {
            if (_held == null) return false;
            if (ler == _held) return true;

            try
            {
                return Limbs.CreatureId(Limbs.CreatureOf(ler)) ==
                       Limbs.CreatureId(Limbs.CreatureOf(_held));
            }
            catch { return false; }
        }

        private static void MeasureBox(LimbEffectorReceiver ler, Vector3 at, Quaternion held)
        {
            _boxCentre  = Vector3.zero;
            _boxExtents = Vector3.one * 0.05f;

            var inverse = Quaternion.Inverse(held);
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;

            try
            {
                foreach (var col in ler.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null) continue;

                    var box = col.TryCast<BoxCollider>();

                    Vector3 centre, extents;
                    bool own = box != null;

                    if (own) { centre = box.center; extents = box.size * 0.5f; }
                    else
                    {
                        var b = col.bounds;
                        centre  = b.center;
                        extents = b.extents;
                    }

                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = centre + Vector3.Scale(extents,
                            new Vector3((c & 1) == 0 ? -1f : 1f,
                                        (c & 2) == 0 ? -1f : 1f,
                                        (c & 4) == 0 ? -1f : 1f));

                        Vector3 world = own ? col.transform.TransformPoint(corner) : corner;
                        Vector3 local = inverse * (world - at);

                        if (!any) { min = max = local; any = true; continue; }

                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }
            }
            catch { }

            if (!any) return;

            _boxCentre  = (min + max) * 0.5f;
            _boxExtents = Vector3.Max((max - min) * 0.5f, Vector3.one * 0.005f);

            Diag.Log("suture",
                $"box {_boxExtents * 2f:0.000}m about {_boxCentre:0.000}");
        }

        private static Vector3 ContactFace(Quaternion rotation, Vector3 into, out Vector3 outward)
        {
            outward = Vector3.down;

            float   best = float.MaxValue;
            Vector3 face = _boxCentre;

            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = Vector3.zero;
                    n[axis] = sign;

                    float facing = Vector3.Dot(rotation * n, into);
                    if (facing >= best) continue;

                    best    = facing;
                    outward = n;
                    face    = _boxCentre + n * _boxExtents[axis];
                }

            return face;
        }

        private static Vector3 Extent(LimbEffectorReceiver ler, Vector3 anchor)
        {
            Vector3 best    = anchor;
            float   bestSqr = 0f;

            try
            {
                foreach (var col in ler.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null) continue;

                    var b = col.bounds;
                    Vector3 far = b.center + Vector3.Scale(b.extents,
                        new Vector3(Mathf.Sign(b.center.x - anchor.x),
                                    Mathf.Sign(b.center.y - anchor.y),
                                    Mathf.Sign(b.center.z - anchor.z)));

                    float sqr = (far - anchor).sqrMagnitude;
                    if (sqr <= bestSqr) continue;

                    best    = far;
                    bestSqr = sqr;
                }
            }
            catch { }

            return best;
        }

        private static string NameOf(LimbEffectorReceiver ler)
        {
            try
            {
                var parent = ler.transform.parent;
                return parent != null ? parent.name : ler.name;
            }
            catch { return "limb"; }
        }
    }

    [HarmonyPatch(typeof(Il2CppLVA.Limbs.LimbPhysics),
                  nameof(Il2CppLVA.Limbs.LimbPhysics.OnCollisionEnter))]
    internal static class PatchSutureImpact
    {
        static bool Prefix(Il2CppLVA.Limbs.LimbPhysics __instance, Collision __0)
        {
            try
            {
                Rigidbody mine = Native.Body(__instance);
                Rigidbody hit  = __0 != null ? __0.rigidbody : null;

                bool held = SutureTool.Settling(mine, hit);

                if (Diag.On && (held || SutureTool.Watching(mine, hit)))
                {
                    SutureTool.NoteImpact(Diag.Name(__instance), Diag.Name(hit),
                                          __0 != null ? __0.impulse.magnitude : 0f, held);
                }

                return !held;
            }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(Il2CppLVA.Limbs.LimbPhysics),
                  nameof(Il2CppLVA.Limbs.LimbPhysics.OnJointBreak))]
    internal static class PatchJointBreakReport
    {
        static void Prefix(Il2CppLVA.Limbs.LimbPhysics __instance, float __0)
        {
            if (!Diag.On) return;
            Diag.Log("suture", $"JOINT BROKE on {Diag.Name(__instance)} at force {__0:0}");
        }
    }
}
