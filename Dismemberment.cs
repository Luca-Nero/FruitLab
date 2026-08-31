using HarmonyLib;
using Il2CppEffectors;
using Il2CppLVA.Limbs;
using MelonLoader;
using System;
using UnityEngine;

namespace FruitLab
{
    /// <summary>
    /// Tells FruitLab when a limb comes apart.
    ///
    /// A limb in this game is a voxel mesh, and dismemberment is mesh separation:
    /// when destruction disconnects a group of voxels from the rest,
    /// SeparationPerformer hands the groups to
    /// <c>LimbDismembermentModule.CreateNewLimbsFromNewMeshesData</c> (v0.1:
    /// <c>hhw</c>, see NAMES.md), which builds a *brand new limb* for each one from
    /// the limb prefab.
    ///
    /// That is the whole reason anything an item did to a limb vanishes when it is
    /// severed. SeparatedMeshData carries voxel *indexes* and nothing else, so the
    /// fragment comes out of the prefab as untouched flesh: no colour an item
    /// painted, and no LimbEffectorReceiver an item was holding on to. From the
    /// item's point of view the flesh it was working on stopped existing and an
    /// identical, pristine piece appeared in its place.
    ///
    /// Subscribers get the limb that split, not the pieces — the pieces are not
    /// necessarily finished being built when this fires, so anything wanting them
    /// should look a moment later.
    /// </summary>
    internal static class Dismemberment
    {
        /// Raised with the limb whose mesh has just been separated.
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
        /// Postfix, so the new limbs exist by the time anyone is told. They are not
        /// necessarily usable yet — colliders in particular come up a frame or two
        /// later — which is why this reports the split rather than the pieces.
        static void Postfix(LimbDismembermentModule __instance) => Dismemberment.Raise(__instance);
    }
}
