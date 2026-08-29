using Il2Cpp;
using Il2CppActiveRagdoll.Scripts;
using MelonLoader;
using System;
using UnityEngine;

namespace FruitLab
{
    /// <summary>
    /// Shared helpers for the humanoid puppeteer — the animation layer that decides how
    /// a body carries itself, as distinct from the LVA graph (<see cref="Vitals"/>) that
    /// decides whether it is alive at all.
    ///
    /// The two are only loosely coupled, and the seams show wherever the game assumes
    /// death is final. Nothing here is about health; it is about undoing the one-way
    /// switches that death throws.
    /// </summary>
    internal static class Puppeteer
    {
        /// Gives a revived body its feet back.
        ///
        /// When consciousness reaches zero the game runs
        /// <c>FootFrictionControl.DisableOnZeroCognitionLevel</c>, which swaps both feet
        /// to a zero-friction physics material so the corpse slides rather than catching
        /// on the ground. There is no counterpart that ever swaps it back — the game has
        /// no notion of getting up again — so a revived body walks on ice: the legs step,
        /// the feet skate out from under it, and it never goes anywhere.
        ///
        /// <c>SetDefaultFriction</c> is the game's own restore, used when a creature
        /// stands normally. The walk cycle's alternating SetLeft/SetRight takes over from
        /// there.
        public static bool RestoreFootFriction(Transform creatureRoot)
        {
            if (creatureRoot == null) return false;

            bool restored = false;

            foreach (var control in creatureRoot.GetComponentsInChildren<FootFrictionControl>(true))
            {
                if (control == null) continue;

                try
                {
                    // m_isNeeded gates the control, and death may latch it off. Only
                    // force it on for a creature that actually tracks two feet —
                    // switching it on for something with no feet to control would be
                    // asking the game to dereference what it never assigned.
                    if (!control.m_isNeeded && control.php != null && control.phq != null)
                        control.m_isNeeded = true;

                    control.SetDefaultFriction();
                    restored = true;
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"[FruitLab] Foot friction restore failed: {e.Message}");
                }
            }

            return restored;
        }
    }
}
