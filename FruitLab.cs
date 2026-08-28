using HarmonyLib;
using FruitLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FruitLab.Core), "FruitLab", "1.0.0", "LucaNero")]
[assembly: MelonGame]

namespace FruitLab
{
    // ── Mod entry-point ───────────────────────────────────────────────────────────
    public class Core : MelonMod
    {
        public static bool HealingActive;
        public static bool KeepAwake;
        public static bool PatchesDisabled;

        public override void OnInitializeMelon()
        {
            HarmonyInstance.PatchAll();
            ConfigLoader.Load();
            FruitMenu.Register("Do No Harm", ConfigLoader.IniPath, typeof(Config));
            LoggerInstance.Msg("DoNoHarm Loaded.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(Config.SyringeKey)) HealingSyringe.Throw();
            if (Input.GetKeyDown(Config.RecallKey))  HealingSyringe.RecallAll();
            HealingSyringe.UpdatePositions();
        }

        public override void OnFixedUpdate() => HealingSyringe.FixedTick();
    }

    [HarmonyPatch(typeof(rx), nameof(rx.gqz))]
    internal static class PatchRxGqz
    {
        static bool Prefix()
        {
            if (DoNoHarmMod.PatchesDisabled) return true;
            if (DoNoHarmMod.KeepAwake) return false;
            return true;
        }
    }

}
