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
    internal static class Items
    {
        public static void Register()
        {
            Toolbar.Register();
            RotSyringe.Register();
            Diag.Register();
        }

        public static void Update()
        {
            if (!FruitLib.FruitMenu.BlocksGameplayInput && Input.GetKeyDown(Config.RecallKey))
                RecallAll();

            Toolbar.OnUpdate();

            HealingSyringe.OnUpdate();
            LazarusSyringe.OnUpdate();
            RotSyringe.OnUpdate();
            SutureTool.OnUpdate();
            VitalsMonitor.OnUpdate();

            Diag.Tick();
        }

        public static void FixedUpdate()
        {
            HealingSyringe.OnFixedUpdate();
            LazarusSyringe.OnFixedUpdate();
            RotSyringe.OnFixedUpdate();
        }

        public static void SceneReload()
        {
            Toolbar.OnSceneReload();
            HealingSyringe.OnSceneReload();
            LazarusSyringe.OnSceneReload();
            RotSyringe.OnSceneReload();
            SutureTool.OnSceneReload();
            VitalsMonitor.OnSceneReload();
            Diag.Stop();
            FruitLabHud.Reset();
        }

        public static void RecallAll()
        {
            HealingSyringe.RecallAll();
            LazarusSyringe.RecallAll();
            RotSyringe.RecallAll();
        }

        public static void DrawGUI()
        {
            if (FruitLib.FruitMenu.BlocksGameplayInput) return;

            VitalsMonitor.OnGUI();
            SutureTool.OnGUI();
        }

        public static bool AnyHoldingOrgans =>
            HealingSyringe.AnyPassRunning || LazarusSyringe.AnyPassRunning;

        public static bool HoldsOrganTeardown(Transform organ) =>
            HealingSyringe.HoldsOrganTeardown(organ) ||
            LazarusSyringe.HoldsOrganTeardown(organ);
    }

    // ── Mod entry-point ───────────────────────────────────────────────────────────
    public class Core : MelonMod
    {
        public const string Version = "1.0.0";

        // ── FruitLib dependency ──────────────────────────────────────────────
        private const int LibMajor = 2, LibMinor = 1, LibPatch = 0;
        private bool _active;

        public override void OnInitializeMelon()
        {
            _active = FruitGate.Check("FruitLab", LibMajor, LibMinor, LibPatch);
            if (!_active) return;

            HarmonyInstance.PatchAll();
            ConfigLoader.Load();

            FruitMenu.Register("FruitLab", ConfigLoader.IniPath, typeof(Config));
            Items.Register();
            FruitUpdateCheck.Register("FruitLab", Version, "Luca-Nero", "FruitLab");

            LoggerInstance.Msg($"FruitLab v{Version} loaded.");
        }

        public override void OnUpdate()
        {
            if (_active) Items.Update();
        }

        public override void OnFixedUpdate()
        {
            if (_active) Items.FixedUpdate();
        }

        public override void OnGUI()
        {
            if (_active) Items.DrawGUI();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_active) Items.SceneReload();
        }
    }

    [HarmonyPatch(typeof(rx), nameof(rx.gqz))]
    internal static class PatchOrganDestroyLVA
    {
        static bool Prefix(rx __instance)
        {
            if (!Items.AnyHoldingOrgans) return true;
            try { return !Items.HoldsOrganTeardown(__instance.transform); }
            catch { return true; }
        }
    }
}
