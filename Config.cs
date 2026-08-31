using FruitLib;
using UnityEngine;

namespace FruitLab
{
    internal static class Config
    {
        // ── Controls ──────────────────────────────────────────────────────────────
        [MenuCategory("Controls")] public static KeyCode RecallKey = KeyCode.X;

        // ── Healing Syringe ───────────────────────────────────────────────────────
        [MenuCategory("Healing Syringe")] public static float ThrowSpeed       = 16f;
        [MenuCategory("Healing Syringe")] public static float StickRadius     = 0.05f;
        [MenuCategory("Healing Syringe")] public static float SpentLifetime   = 30f;
        [MenuCategory("Healing Syringe")] public static float HealTickInterval = 0.05f;
        [MenuCategory("Healing Syringe")] public static float HealSignal       = 1000000f;
        [MenuCategory("Healing Syringe")] public static float HealWaveSpeed    = 0.25f;
        [MenuCategory("Healing Syringe")] public static float HealWorldSpeed   = 3f;

        // ── Lazarus Syringe ───────────────────────────────────────────────────────
        [MenuCategory("Lazarus Syringe")] public static float LazarusDuration = 30f;
        [MenuCategory("Lazarus Syringe")] public static float LazarusInterval = 0.1f;

        // ── Flesh Rot Syringe ─────────────────────────────────────────────────────
        [MenuCategory("Flesh Rot")] public static float RotSpreadSeconds = 2.5f;
        [MenuCategory("Flesh Rot")] public static float RotSpreadAt      = 0.55f;
        [MenuCategory("Flesh Rot")] public static float RotWaveSpeed     = 0.35f;
        [MenuCategory("Flesh Rot")] public static float RotNecrosisLag   = 4f;
        [MenuCategory("Flesh Rot")] public static float RotDeathLag      = 3f;
        [MenuCategory("Flesh Rot")] public static float RotTickInterval  = 0.08f;
        [MenuCategory("Flesh Rot")] public static float RotDamage        = 50000f;
        [MenuCategory("Flesh Rot")] public static float RotMarkDamage    = 1f;
        [MenuCategory("Flesh Rot")] public static float RotPitting       = 6f;

        // ── Suture Tool ───────────────────────────────────────────────────────────
        [MenuCategory("Suture Tool")] public static float SutureAimRange      = 8f;
        [MenuCategory("Suture Tool")] public static float SutureCarryDistance = 1.1f;
        [MenuCategory("Suture Tool")] public static float SutureTurnStep      = 7.5f;
        [MenuCategory("Suture Tool")] public static KeyCode SutureRollKey     = KeyCode.LeftAlt;
        [MenuCategory("Suture Tool")] public static KeyCode SutureTiltKey     = KeyCode.LeftControl;
        [MenuCategory("Suture Tool")] public static float SutureCarryRight    = 0.34f;
        [MenuCategory("Suture Tool")] public static float SutureCarryUp       = 0.20f;
        [MenuCategory("Suture Tool")] public static float SutureSeamOffset    = 0.02f;
        [MenuCategory("Suture Tool")] public static float SutureBreakForce    = 0f;
        [MenuCategory("Suture Tool")] public static bool  SutureSeamCollision = false;
        [MenuCategory("Suture Tool")] public static bool  SutureNative        = true;
        [MenuCategory("Suture Tool")] public static bool  SutureGraft         = true;
        [MenuCategory("Suture Tool")] public static bool  SutureAdopt         = true;
        [MenuCategory("Suture Tool")] public static float SutureSettle        = 0.4f;
        [MenuCategory("Suture Tool")] public static bool  SutureGhost         = true;
        [MenuCategory("Suture Tool")] public static bool  SutureAlign         = true;

        // ── Vitals Monitor ────────────────────────────────────────────────────────
        [MenuCategory("Vitals Monitor")] public static float VitalsRange = 25f;

        // ── Debug ─────────────────────────────────────────────────────────────────
        [MenuCategory("Debug")] public static bool  LogVitals       = false;
        [MenuCategory("Debug")] public static bool  LogDiagnostics  = false;
        [MenuCategory("Debug")] public static float DiagWindow      = 2.5f;
    }
}
