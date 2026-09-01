using FruitLib;
using UnityEngine;

namespace FruitLab
{
    internal static class Config
    {
        // ── Controls ──────────────────────────────────────────────────────────────
        [MenuCategory("Controls")] public static KeyCode RecallKey = KeyCode.X;
        [MenuCategory("Controls")] public static KeyCode SutureKeyFacePrev = KeyCode.Keypad4;
        [MenuCategory("Controls")] public static KeyCode SutureKeyFaceNext = KeyCode.Keypad6;
        [MenuCategory("Controls")] public static KeyCode SutureKeySpinLeft = KeyCode.Keypad7;
        [MenuCategory("Controls")] public static KeyCode SutureKeySpinRight = KeyCode.Keypad9;

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
        public static bool  RotEnabled       = false;
        public static float RotSpreadSeconds = 2.5f;
        public static int   RotSeamReach     = 5;
        public static float RotBlackenAfter  = 2.5f;
        public static float RotDestroyAfter  = 2f;
        public static float RotTickInterval  = 0.08f;
        public static float RotDamage        = 50000f;
        public static float RotMarkDamage    = 1f;
        public static int   RotMinPiece      = 12;

        // ── Suture Tool ───────────────────────────────────────────────────────────
        [MenuCategory("Suture Tool")] public static float SutureAimRange      = 8f;
        [MenuCategory("Suture Tool")] public static float SutureTurnStep      = 90f;
        [MenuCategory("Suture Tool")] public static float SutureGlide         = 0.22f;
        [MenuCategory("Suture Tool")] public static float SutureSeamOffset    = 0.02f;
        [MenuCategory("Suture Tool")] public static float SutureBreakForce    = 0f;
        [MenuCategory("Suture Tool")] public static float SutureSettle = 0.4f;
         public static bool  SutureSeamCollision = false;
         public static bool  SutureNative        = true;
         public static bool  SutureGraft         = true;
         public static bool  SutureAdopt         = true;
         public static bool  SutureGhost         = true;
         public static bool  SutureAlign         = true;

        // ── Syringe model ─────────────────────────────────────────────────────────
         public static bool  SyringeModel      = true;
         public static float SyringeScale      = 1f;
         public static float SyringeGlassAlpha = 0.35f;
         public static float SyringePlungerDraw = -0.075f;

        // ── Vitals Monitor ────────────────────────────────────────────────────────
        [MenuCategory("Vitals Monitor")] public static float VitalsRange = 25f;

        [MenuCategory("Vitals Monitor")] public static int   VitalsSpecialOdds = 1000;
        [MenuCategory("Vitals Monitor")] public static int   VitalsBpmRestMin  = 62;
        [MenuCategory("Vitals Monitor")] public static int   VitalsBpmRestMax  = 84;

        // ── Debug ─────────────────────────────────────────────────────────────────
        [MenuCategory("Debug")] public static bool  LogVitals       = false;
        [MenuCategory("Debug")] public static bool  LogDiagnostics  = false;
        [MenuCategory("Debug")] public static bool VitalsRaw = false;
        public static bool VitalsNames = true;
        [MenuCategory("Debug")] public static float DiagWindow      = 2.5f;
    }
}
