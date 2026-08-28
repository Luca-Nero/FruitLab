using FruitLib;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FruitLab.Core), "FruitLab", FruitLab.Core.Version, "Luca_Nero")]
[assembly: MelonGame]
[assembly: MelonOptionalDependencies("FruitLib")]

namespace FruitLab
{
    // ── Mod entry-point ───────────────────────────────────────────────────────────
    public class Core : MelonMod
    {
        public const string Version = "1.0.0";

        // ── FruitLib dependency ──────────────────────────────────────────────
        private const int LibMajor = 2, LibMinor = 1, LibPatch = 0;
        private bool _active;

        public static bool HealingActive;
        public static bool KeepAwake;
        public static bool PatchesDisabled;

        public override void OnInitializeMelon()
        {
            _active = FruitGate.Check("FruitLab", LibMajor, LibMinor, LibPatch);
            if (!_active) return;

            Init();
        }

        public void Init()
        {
            HarmonyInstance.PatchAll();
            ConfigLoader.Load();

            FruitMenu.Register("FruitLab", ConfigLoader.IniPath, typeof(Config));


            FruitUpdateCheck.Register("FruitLab", Version, "Luca-Nero", "FruitLab");
            LoggerInstance.Msg($"FruitLab v{Version} loaded.");
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
            if (FruitLab.Core.PatchesDisabled) return true;
            if (FruitLab.Core.KeepAwake) return false;
            return true;
        }
    }

}
