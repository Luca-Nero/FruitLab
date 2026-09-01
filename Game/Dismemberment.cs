using HarmonyLib;
using Il2CppEffectors;
using Il2CppLVA.Limbs;
using MelonLoader;
using System;
using UnityEngine;

namespace FruitLab
{
    internal static class Dismemberment
    {
        public static event Action<LimbEffectorReceiver> Split;

        internal static void Raise(LimbDismembermentModule module)
        {
            var handler = Split;
            if (handler == null || module == null) return;

            try
            {
                var ler = Limbs.Of(module.gameObject);
                if (ler != null) handler(ler);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FruitLab] Dismemberment notification failed: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(LimbDismembermentModule), nameof(LimbDismembermentModule.hhw))]
    internal static class PatchCreateNewLimbs
    {
        static void Postfix(LimbDismembermentModule __instance) => Dismemberment.Raise(__instance);
    }
}
