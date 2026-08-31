using FruitLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // The syringe rack — one toolbar slot holding every syringe, cycled on the
    // mousewheel the way GunsGunsGuns cycles its weapons.
    //
    // Three syringes meant three slots on a toolbar that is not ours and does not
    // grow, and they are the same tool with different contents anyway: you pick one
    // up, you point it at a body, you throw it. So the slot is the rack and the
    // wheel picks the dose. The Vitals Monitor keeps its own slot deliberately — it
    // is an instrument, not a syringe, and wanting to read a body while holding a
    // dose is the normal case rather than the exception.
    //
    // Each syringe stays self-contained: it owns its name, its colour and its
    // Throw. Adding one is writing its file and adding a line to the list below.
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class SyringeRack
    {
        public const string ItemId = "FruitLab:Syringes";

        private sealed class Slot
        {
            public string Label;
            public Color  Colour;
            public Action Throw;
        }

        private static readonly List<Slot> _rack = new List<Slot>
        {
            new Slot { Label = HealingSyringe.DisplayName,
                       Colour = HealingSyringe.IconColor, Throw = HealingSyringe.Throw },
            new Slot { Label = LazarusSyringe.DisplayName,
                       Colour = LazarusSyringe.IconColor, Throw = LazarusSyringe.Throw },
            new Slot { Label = RotSyringe.DisplayName,
                       Colour = RotSyringe.IconColor,     Throw = RotSyringe.Throw },
        };

        private static FruitToolbarItem _item;
        private static int  _index;
        private static bool _equipped;

        private static Slot Current => _rack[Mathf.Clamp(_index, 0, _rack.Count - 1)];

        // ══════════════════════════════════════════════════════════════════════════
        // Toolbar item
        // ══════════════════════════════════════════════════════════════════════════

        public static void Register()
        {
            var start = Current;

            _item = new FruitToolbarItem
            {
                Id           = ItemId,
                Name         = start.Label,
                Icon         = FruitToolbar.MakeSolidIcon(start.Colour),
                OnSelected   = OnSelected,
                OnDeselected = OnDeselected,
            };

            FruitToolbar.Register(_item);
        }

        private static void OnSelected(int slot)
        {
            _equipped = true;
            MelonLogger.Msg($"[FruitLab] {Current.Label} equipped (slot {slot + 1}).");
        }

        private static void OnDeselected(int slot) => _equipped = false;

        /// FruitToolbar drops its selection on a scene change without dispatching the
        /// deselect callback, so the equipped flag has to be cleared here or the rack
        /// stays armed on left click into the next scene. The dose carries over: it is
        /// a choice the player made, not scene state.
        public static void OnSceneReload() => _equipped = false;

        // ══════════════════════════════════════════════════════════════════════════
        // Frame loop
        // ══════════════════════════════════════════════════════════════════════════

        public static void OnUpdate()
        {
            // Clicks and scrolls aimed at FruitLib's menu are not gameplay input.
            if (!_equipped || FruitMenu.BlocksGameplayInput) return;

            Cycle();

            if (Input.GetMouseButtonDown(0)) Current.Throw();
        }

        /// The slot itself is the readout — its label and colour change under the
        /// cursor — so this says nothing to the console.
        private static void Cycle()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            int step = scroll > 0f ? 1 : -1;
            _index = ((_index + step) % _rack.Count + _rack.Count) % _rack.Count;

            var now = Current;
            _item?.SetDisplay(now.Label, FruitToolbar.MakeSolidIcon(now.Colour));
        }
    }
}
