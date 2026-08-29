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

        // ── Vitals Monitor ────────────────────────────────────────────────────────
        [MenuCategory("Vitals Monitor")] public static float VitalsRange = 25f;

        // ── Debug ─────────────────────────────────────────────────────────────────
        [MenuCategory("Debug")] public static bool LogVitals = false;
    }
}
