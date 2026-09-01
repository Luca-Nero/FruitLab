using Il2Cpp;
using Il2CppActiveRagdoll.Scripts;
using MelonLoader;
using System;
using UnityEngine;

namespace FruitLab
{
    internal static class Puppeteer
    {
        public static bool RestoreFootFriction(Transform creatureRoot)
        {
            if (creatureRoot == null) return false;

            bool restored = false;

            foreach (var control in creatureRoot.GetComponentsInChildren<FootFrictionControl>(true))
            {
                if (control == null) continue;

                try
                {
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
