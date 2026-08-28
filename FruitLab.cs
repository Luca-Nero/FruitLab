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
    /// <summary>
    /// The item roster. Adding a FruitLab item means writing its file and adding one
    /// line to each method here — <see cref="Core"/> itself never changes.
    /// </summary>
    internal static class Items
    {
        public static void Register()    => HealingSyringe.Register();
        public static void Update()      => HealingSyringe.OnUpdate();
        public static void FixedUpdate() => HealingSyringe.OnFixedUpdate();
        public static void SceneReload() => HealingSyringe.OnSceneReload();

        /// Cheap gate for <see cref="PatchOrganDestroyLVA"/>: is any item doing
        /// something that needs organs kept alive?
        public static bool AnyHoldingOrgans => HealingSyringe.AnyPassRunning;

        public static bool HoldsOrganTeardown(Transform organ) =>
            HealingSyringe.HoldsOrganTeardown(organ);
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

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_active) Items.SceneReload();
        }
    }

    /// <summary>
    /// Holds off <c>AbstractOrgan.DestroyLVA()</c> (v0.1: <c>rx.gqz</c>, see
    /// NAMES.md) for organs belonging to a creature an item is working on, so the
    /// death cascade cannot tear down an organ that is being brought back.
    ///
    /// Scoped to those creatures, and only while the work is actually running. This
    /// patch used to suppress teardown globally for as long as any syringe was
    /// attached, which also swallowed legitimate destruction — deleting a creature
    /// or unloading a scene silently leaked organs.
    /// </summary>
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
