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
    //
    // The game has a reattach of its own — CreatureHierarchyOperationsModule's
    // TryAddAsNative, the exact inverse of the context menu's Detach Limb — and it
    // does not work for this. "Native" is the catch: a severed limb stops being one
    // of that creature's own, so the module refuses to take it back. Tested on an arm
    // cut off at the shoulder and offered straight back to the body it came from:
    // refused. It is for limbs that were never removed, not for putting one back on.
    //
    // So this does it the owner-agnostic way, and gets a better tool out of it. Any
    // limb goes on any body, anywhere you click — through CreatureNodesHierarchy's
    // AttachNodeAsThirdparty, because grafting a limb that is not a creature's own is
    // something the game supports outright: there is a ThirdpartyNodeAttachProtocol
    // sitting beside the two native ones. That protocol is also what notifies the
    // creature's limb listeners, and the puppeteer's listener is one of them, so it is
    // the difference between a limb bolted on and a limb the body drives.
    //
    // Carrying is what makes it usable rather than merely possible. A limb picked up
    // goes kinematic with its colliders off and rides in front of the camera, so it
    // holds its shape, holds still, and passes through the world while you line the
    // shoulder up with the socket. Nothing has to be frozen first. That also removes
    // the thing that was wrecking ragdolls: a solid limb shoved into a body resolves
    // the overlap explosively, and a limb that is not solid until the moment it is
    // sewn on never generates that impulse.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class SutureTool
    {
        public const string ItemId      = "FruitLab:Suture";
        public const string DisplayName = "Suture Tool";

        private static readonly Color IconColor = new Color(0.55f, 0.80f, 0.74f, 1f);

        private static bool _equipped;

        /// The limb being carried, if any.
        private static LimbEffectorReceiver _held;
        private static string     _heldName;
        private static float      _carryDistance;
        /// The assembly's rotation in camera space when it was picked up, so it turns
        /// with the head instead of hanging in a fixed world orientation.
        private static Quaternion _carryLocal = Quaternion.identity;

        /// Where the limb attaches and which way it runs, both in the held body's own
        /// space, measured once at pick-up.
        ///
        /// Measured *before* the colliders go off, because a disabled collider reports
        /// no bounds — read them while carrying and the limb has no size and no
        /// direction, so it would refuse to turn and sew itself on by its middle.
        private static Vector3 _anchorLocal;
        private static Vector3 _axisLocal;

        /// Hand-set rotation, in camera space, on top of whatever the view is doing.
        /// Roll spins the limb about the way you are looking; tilt tips it. Both survive
        /// placement, because aligning to a surface only pins the limb's long axis and
        /// leaves the turn about it alone.
        private static float _carryRoll;
        private static float _carryTilt;

        /// What was suspended to carry it, and what it was before, so putting it down
        /// restores rather than guesses.
        private sealed class Suspended
        {
            public Rigidbody Rb;
            public bool      WasKinematic;
        }

        /// Bodies that have just been sewn on, and until when. Collision damage is held
        /// off for them while the seam settles — see PatchSutureImpact.
        private static readonly HashSet<int> _settling = new HashSet<int>();
        private static float _settleUntil;

        private static readonly List<Suspended> _carried = new List<Suspended>();
        private static readonly List<Collider>  _ghosted = new List<Collider>();
        private static readonly List<Rigidbody> _scratch = new List<Rigidbody>();
        private static readonly List<LimbEffectorReceiver> _watched =
            new List<LimbEffectorReceiver>();
        /// The carried limb and everything hanging off it, in the order the creature
        /// lists them — which is parent-first, so adopting down the chain works.
        private static readonly List<LimbEffectorReceiver> _assembly =
            new List<LimbEffectorReceiver>();
        private static readonly List<LimbEffectorReceiver> _bodyLimbs =
            new List<LimbEffectorReceiver>();

        // ══════════════════════════════════════════════════════════════════════════
        // Toolbar item
        // ══════════════════════════════════════════════════════════════════════════

        public static void Register()
        {
            FruitToolbar.Register(new FruitToolbarItem
            {
                Id           = ItemId,
                Name         = DisplayName,
                Icon         = FruitToolbar.MakeSolidIcon(IconColor),
                OnSelected   = OnSelected,
                OnDeselected = OnDeselected,
            });
        }

        private static void OnSelected(int slot)
        {
            _equipped = true;
            MelonLogger.Msg($"[FruitLab] Suture Tool equipped (slot {slot + 1}).");
        }

        /// Putting the tool away puts the limb down with it. A limb left carried while
        /// the tool is holstered would be an invisible, intangible, weightless thing
        /// following the camera around.
        private static void OnDeselected(int slot)
        {
            _equipped = false;
            Release();
        }

        /// FruitToolbar drops its selection on a scene change without dispatching the
        /// deselect callback, so the flag has to be cleared here or the tool stays
        /// armed on left click into the next scene.
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
            // Clicks aimed at FruitLib's menu are not gameplay input.
            if (!_equipped || FruitMenu.BlocksGameplayInput) return;

            if (_held != null)
            {
                // A carried limb can still be destroyed, and reading anything off a
                // destroyed component throws rather than returning null.
                bool gone;
                try { gone = _held.transform == null; } catch { gone = true; }
                if (gone) { Forget(); return; }

                if (Input.GetMouseButtonDown(1))
                {
                    MelonLogger.Msg($"[FruitLab] Put {_heldName} back down.");
                    Release();
                    return;
                }

                Steer();
                Carry();
            }

            if (!Input.GetMouseButtonDown(0)) return;

            if (_held == null) Pick();
            else               Place();
        }

        public static void OnGUI()
        {
            if (!_equipped || _held == null) return;

            var style = FruitLabHud.Text(13);
            var prev  = GUI.color;

            string line = $"Carrying {_heldName} — click to sew it on, scroll to reach, " +
                          $"{Config.SutureRollKey}+scroll to roll, " +
                          $"{Config.SutureTiltKey}+scroll to tilt, right click to drop";

            if (_carryRoll != 0f || _carryTilt != 0f)
                line += $"   [roll {_carryRoll:0}°  tilt {_carryTilt:0}°]";

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

            // Unheld covers a creature's root limb too, which is exactly right: a
            // pelvis nothing is holding is as free to be sewn onto something as a
            // severed arm is, and there is no reason to be precious about which.
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

            Suspend(ler);

            _carryDistance = Mathf.Clamp(Config.SutureCarryDistance, 0.3f,
                                         Mathf.Max(Config.SutureAimRange, 0.5f));

            _carryLocal = Quaternion.Inverse(cam.transform.rotation) * held;
            _carryRoll  = 0f;
            _carryTilt  = 0f;

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
                // The assembly root is named because the counts have been surprising: a
                // lone head reported ten rigidbodies, and a head prefab has one. Whatever
                // is being scooped up gets suspended, carried and handed over with the
                // limb, so it matters what it is.
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

        /// One wheel, three jobs, chosen by what you are holding down: reach by default,
        /// roll or tilt with a modifier.
        ///
        /// Modifiers rather than more keys because placing a limb is a two-handed job
        /// already — the mouse is aiming and the wheel is adjusting, and reaching for a
        /// letter key means letting go of the aim.
        private static void Steer()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float step = Mathf.Max(Config.SutureTurnStep, 0.5f);

            if (Input.GetKey(Config.SutureRollKey))
            {
                _carryRoll = Mathf.Repeat(_carryRoll + scroll * step, 360f);
                return;
            }

            if (Input.GetKey(Config.SutureTiltKey))
            {
                _carryTilt = Mathf.Clamp(_carryTilt + scroll * step, -180f, 180f);
                return;
            }

            _carryDistance = Mathf.Clamp(_carryDistance + scroll * 0.12f, 0.3f,
                                         Mathf.Max(Config.SutureAimRange, 0.5f));
        }

        /// Holds the assembly out in front of the camera by its own attach point, so
        /// the end that has to meet the socket is the end you are steering.
        private static void Carry()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var t = cam.transform;

            // Held up and to the side rather than dead ahead, so the limb is not parked
            // on top of the socket you are lining it up with. Scaled by how far out it
            // is held, which keeps it in the same place on screen however far you push
            // it. The offset is only where the limb *looks*, and has no say in where it
            // ends up: placing it takes the attach point to the spot you clicked.
            Vector3 target = t.position
                           + t.forward * _carryDistance
                           + (t.right * Config.SutureCarryRight +
                              t.up    * Config.SutureCarryUp) * _carryDistance;

            var body = Limbs.BodyOf(_held);
            if (body == null) return;

            Quaternion want = t.rotation
                            * Quaternion.AngleAxis(_carryTilt, Vector3.right)
                            * Quaternion.AngleAxis(_carryRoll, Vector3.forward)
                            * _carryLocal;

            Move(Anchor(body), target, want * Quaternion.Inverse(body.rotation));

            Preview(body);
        }

        /// Shows the hologram wherever the crosshair is currently pointing, or hides it
        /// when that is not somewhere a limb can go.
        private static void Preview(Rigidbody body)
        {
            if (!Config.SutureGhost) return;

            if (!Aim(out RaycastHit hit)) { SutureGhost.Hide(); return; }

            var parent = Limbs.Of(hit.collider.gameObject);
            if (parent == null) { SutureGhost.Hide(); return; }

            Solve(hit, body, out _, out _, out Vector3 pos, out Quaternion rot);
            SutureGhost.Show(pos, rot, !Carrying(parent) && Limbs.BodyOf(parent) != null);
        }

        /// Where the limb ends up for a given aim: the seam it attaches at, the turn
        /// that gets it there, and the pose its own body lands in.
        ///
        /// One place, used by both the hologram and the actual placement, because a
        /// preview that is computed differently from the thing it previews is worse
        /// than no preview.
        private static void Solve(RaycastHit hit, Rigidbody childBody,
                                  out Vector3 seam, out Quaternion turn,
                                  out Vector3 position, out Quaternion rotation)
        {
            // The seam sits a little off the surface, the way the game's own limb
            // joints do — sockets are set back from where two limbs meet rather than
            // sitting on the skin, which is what keeps them from grinding through each
            // other. Clicking a surface gives a point exactly *on* it, so the whole
            // join is nudged out along the normal. Negative sinks it in.
            Vector3 normal = hit.normal.sqrMagnitude > 1e-6f
                           ? hit.normal.normalized : Vector3.up;
            seam = hit.point + normal * Config.SutureSeamOffset;

            turn = Quaternion.identity;
            if (Config.SutureAlign && _axisLocal != Vector3.zero)
            {
                // Point the limb out of the surface rather than into it: the axis from
                // its attach point to its far end is which way the limb runs, and the
                // surface normal is which way there is room for it.
                turn = Quaternion.FromToRotation(childBody.rotation * _axisLocal, normal);
            }

            Vector3 anchor = Anchor(childBody);
            position = seam + turn * (childBody.position - anchor);
            rotation = turn * childBody.rotation;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Sew on
        // ══════════════════════════════════════════════════════════════════════════

        private static void Place()
        {
            // The carried limb is intangible, so it cannot be what the aim hits.
            if (!Aim(out RaycastHit hit)) return;

            var parent = Limbs.Of(hit.collider.gameObject);
            if (parent == null) return;

            // Everything hanging off the carried limb moves with it, so attaching to
            // one of those would be sewing the limb to itself.
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

            Solve(hit, childBody, out Vector3 seam, out Quaternion turn, out _, out _);

            // Watch both bodies across every step. A suture does all of its work inside
            // one frame, so per-frame sampling could only say "something in there did
            // it" — the samples between steps are what name the culprit.
            if (Diag.On)
            {
                _watched.Clear();
                Limbs.CollectBody(_held, _bodyLimbs);
                _watched.AddRange(_bodyLimbs);
                Limbs.CollectBody(parent, _bodyLimbs);
                foreach (var ler in _bodyLimbs)
                    if (!_watched.Contains(ler)) _watched.Add(ler);
                Diag.WatchVoxels("suture", _watched, Mathf.Max(Config.DiagWindow, 0.5f));

                // Everything in reach, once, before anything is touched. An intact limb
                // on the target body is the only reference that makes the sutured
                // limb's readings mean anything.
                Diag.Survey("survey", _watched);

                Diag.Log("suture",
                    $"sewing {_heldName} onto {NameOf(parent)} at {seam:0.00}, " +
                    $"turn {Quaternion.Angle(Quaternion.identity, turn):0}deg");

                // The settings in force, every time. Three rounds of this were spent
                // reading identical-looking symptoms without knowing what the config
                // said — a saved ini outlives the build that wrote it, so the settings
                // a report was produced under are never safe to assume.
                Diag.Log("suture",
                    $"settings: seamOffset {Config.SutureSeamOffset:0.000}m, " +
                    $"seamCollision {Config.SutureSeamCollision}, " +
                    $"settle {Config.SutureSettle:0.00}s, " +
                    $"breakForce {(Config.SutureBreakForce > 0f ? Config.SutureBreakForce.ToString("0") : "none")}, " +
                    $"native {Config.SutureNative}, graft {Config.SutureGraft}, " +
                    $"adopt {Config.SutureAdopt}, align {Config.SutureAlign}");
            }

            string name0 = _heldName;

            Diag.Wiring("before", _held);

            // The physical suture happens either way, always.
            //
            // The native path used to skip it, on the assumption that a limb put back
            // in its own slot would be placed there too. It is not: the hierarchy calls
            // move the node and nothing else, so the limb stayed hanging in mid-air
            // where it had been carried, correctly parented and nowhere near the body.
            // Node membership and physical placement are unrelated here, and the limb
            // needs both regardless of which graft it gets.
            Move(Anchor(childBody), seam, turn);
            Diag.Sample("moved into place");

            string name    = name0;
            bool   jointed = Join(_held, parentBody, seam);
            Diag.Sample("joint wired");

            // Its own socket first. A limb still counted among this body's own belongs
            // in the slot it came out of — that is where its node tag is, and the
            // puppeteer knows limbs only by tag — and the third-party graft strips that
            // standing away, so reaching for it first would quietly demote a limb that
            // had every right to go home.
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

                // Read before the hand-over: taking ownership takes the driver with it,
                // so a head's puppeteer is already gone by the time we would look.
                var carriedDriver = Limbs.PuppeteerOf(_held);

                int taken = Limbs.AdoptAll(_assembly, body);

                Diag.Log("wiring",
                    $"{taken} of {_assembly.Count} limb(s) handed over to {Diag.Body(body)}");

                // Nothing hands the puppeteer over: it cannot be done, see
                // Native.InitializePuppeteer. But adoption runs from the carried half
                // *into* the target's creature, so which half you carry decides which
                // mind survives — and that makes the limitation something you can work
                // around rather than something you are stuck with.
                //
                // Said out loud rather than only in diagnostics, because it is the one
                // piece of knowledge that turns "this body is inert" into "hold it the
                // other way round", and nothing about the tool would suggest it.
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

            // Nothing the limb touches hurts anybody for a moment. A limb sewn onto a
            // chest is inside that chest, and inside whatever the body is lying on, and
            // the frame it becomes solid is a frame of enormous contact impulses. Those
            // go through the game's own collision damage as a sphere of destroyed flesh
            // at the seam — which then disconnects the voxels holding the limb on, so
            // the game severs it again. That is the whole "carves a hole and falls off".
            Settle();

            // Solid first, then unclash, then heavy. Unity resets a collider's ignore
            // list when it is re-enabled, so telling the seam not to collide before
            // that would be undone on the spot. And the mass comes back last, with the
            // joint already wired and everything exactly where it belongs, so there is
            // no error for the solver to correct and nothing to fling.
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

        /// Wires the joint that actually holds the limb on.
        ///
        /// Ordinary Unity, because LimbPhysics.m_joint is an ordinary
        /// ConfigurableJoint. A limb with no joint at all gets one built as a ball
        /// joint: locked against sliding out of its socket, free to swing.
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

                    joint        = rb.gameObject.AddComponent<ConfigurableJoint>();
                    joint.anchor = joint.transform.InverseTransformPoint(seam);
                    made         = true;
                }

                joint.autoConfigureConnectedAnchor = false;
                joint.connectedBody   = parentBody;
                joint.connectedAnchor = parentBody.transform.InverseTransformPoint(seam);

                // Preprocessing is what turns a joint it cannot satisfy into a body
                // flung across the level, and a seam sewn by hand is exactly the case
                // it cannot always satisfy.
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

        /// Excuses the seam from collision, and only the seam.
        ///
        /// Just the limb that was sewn on, against the body it was sewn to — not the
        /// whole assembly. Two limbs meeting at a joint overlap by design, so the one
        /// at the join has to be let off or it grinds against its new neighbour
        /// forever, and the game reads that grinding as impact damage: a sphere of
        /// destroyed flesh at the seam, which disconnects the limb and severs it again.
        ///
        /// Everything hanging off it keeps full collision. An excused *assembly* meant
        /// a sutured arm whose forearm and hand swept through the torso, which is a
        /// worse lie than the one it was fixing — the overlap that actually needs
        /// forgiving is one limb deep.
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

        /// Whether either body is one this tool has recently touched — used only to
        /// decide whether an impact is worth reporting.
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

        /// Marks the assembly as newly sewn on, so PatchSutureImpact holds collision
        /// damage off it while it beds in.
        private static void Settle()
        {
            _settling.Clear();

            foreach (var s in _carried)
            {
                try { if (s.Rb != null) _settling.Add(s.Rb.GetInstanceID()); } catch { }
            }

            _settleUntil = Time.time + Mathf.Max(Config.SutureSettle, 0f);
        }

        /// Notes a collision involving a limb, while a suture is being watched.
        internal static void NoteImpact(string on, string against, float impulse, bool held)
        {
            if (!Diag.On) return;
            Diag.Log("suture",
                $"impact on {on} from {against}, impulse {impulse:0.0}" +
                (held ? " — damage held off" : " — LET THROUGH"));
        }

        /// Whether collision damage is currently being held off one of these bodies.
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

        /// Takes the assembly out of the simulation: kinematic so it holds its shape
        /// and stays where it is put, intangible so it can be pushed through a body
        /// without wrecking it — and so the aim for the second click passes through it.
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

        /// Hands the assembly back to the simulation exactly as it was found.
        private static void Release()
        {
            Solidify();
            Reanimate();
        }

        /// Tangible again.
        private static void Solidify()
        {
            foreach (var col in _ghosted)
            {
                try { if (col != null) col.enabled = true; } catch { }
            }
        }

        /// Heavy again, and let go of.
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

        /// Lets go without touching anything — for when the limb is already gone.
        private static void Forget()
        {
            SutureGhost.Clear();

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

        /// Moves the carried assembly as one rigid piece: turned by <paramref name="turn"/>
        /// about its attach point, then carried so that point lands on the target.
        ///
        /// The whole assembly, always. Moving only the limb itself would leave a
        /// forearm and hand behind at the old shoulder, and the joints between them
        /// would snap the arm straight back out of wherever it was put.
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

        /// Where the carried limb attaches, now — from the offset measured at pick-up,
        /// because the colliders it would otherwise be read from are switched off.
        private static Vector3 Anchor(Rigidbody body) =>
            body.position + body.rotation * _anchorLocal;

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

        /// Every rigidbody that moves with the limb.
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

        /// Whether a limb is part of the assembly currently being carried.
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

        /// The point on a limb furthest from where it attaches — which way it runs.
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
                    // The far corner of the bounds, on the side away from the anchor.
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

    /// <summary>
    /// Holds the game's collision damage off a limb for a moment after it is sutured
    /// on, and off whatever it lands against.
    ///
    /// Both sides, deliberately: Unity raises the collision on each party, so
    /// suppressing only the limb still let the chest take the hit — which is exactly
    /// where the hole was appearing.
    ///
    /// Scoped to the bodies involved and to a deadline in absolute time, so it cannot
    /// leak into a general immunity if something goes wrong mid-suture.
    /// </summary>
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

    /// <summary>
    /// Reports a joint letting go. A sutured limb that falls off has either had its
    /// joint break or been severed by the game, and those look identical from outside.
    /// </summary>
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
