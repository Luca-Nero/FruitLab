using FruitLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    // ══════════════════════════════════════════════════════════════════════════════
    // What FruitLab puts on the toolbar.
    // ══════════════════════════════════════════════════════════════════════════════

    internal sealed class Tool
    {
        public string Label;
        public Color  Colour;

        public Action Use;

        public Action Equip;
        public Action Unequip;

        public Func<bool> UsesWheel;
    }

    internal sealed class ToolRack
    {
        private readonly string     _id;
        private readonly List<Tool> _tools = new List<Tool>();

        private FruitToolbarItem _item;
        private int  _index;
        private bool _equipped;

        public ToolRack(string id) { _id = id; }

        public ToolRack Add(Tool tool)
        {
            if (tool != null) _tools.Add(tool);
            return this;
        }

        private Tool Current =>
            _tools.Count == 0 ? null : _tools[Mathf.Clamp(_index, 0, _tools.Count - 1)];

        public void Register()
        {
            var start = Current;
            if (start == null) return;

            _item = new FruitToolbarItem
            {
                Id           = _id,
                Name         = start.Label,
                Icon         = FruitToolbar.MakeSolidIcon(start.Colour),
                OnSelected   = OnSelected,
                OnDeselected = OnDeselected,
            };

            FruitToolbar.Register(_item);
        }

        private void OnSelected(int slot)
        {
            _equipped = true;
            Current?.Equip?.Invoke();
            MelonLogger.Msg($"[FruitLab] {Current?.Label} equipped (slot {slot + 1}).");
        }

        private void OnDeselected(int slot)
        {
            _equipped = false;
            Current?.Unequip?.Invoke();
        }

        public void OnSceneReload()
        {
            if (_equipped) Current?.Unequip?.Invoke();
            _equipped = false;
        }

        public void OnUpdate()
        {
            if (!_equipped || FruitMenu.BlocksGameplayInput) return;

            var tool = Current;
            if (tool == null) return;

            if (tool.UsesWheel == null || !tool.UsesWheel()) Cycle();

            if (Input.GetMouseButtonDown(0)) tool.Use?.Invoke();
        }

        private void Cycle()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f || _tools.Count < 2) return;

            Current?.Unequip?.Invoke();

            int step = scroll > 0f ? 1 : -1;
            _index = ((_index + step) % _tools.Count + _tools.Count) % _tools.Count;

            var now = Current;
            now?.Equip?.Invoke();
            _item?.SetDisplay(now.Label, FruitToolbar.MakeSolidIcon(now.Colour));
        }
    }

    internal static class Toolbar
    {
        private static readonly List<ToolRack> _racks = new List<ToolRack>();

        public static void Register()
        {
            _racks.Clear();

            var syringes = new ToolRack("FruitLab:Syringes")
                .Add(new Tool
                {
                    Label = HealingSyringe.DisplayName,
                    Colour = HealingSyringe.IconColor,
                    Use = HealingSyringe.Throw,
                })
                .Add(new Tool
                {
                    Label = LazarusSyringe.DisplayName,
                    Colour = LazarusSyringe.IconColor,
                    Use = LazarusSyringe.Throw,
                });

            if (Config.RotEnabled)
                syringes.Add(new Tool
                {
                    Label = RotSyringe.DisplayName,
                    Colour = RotSyringe.IconColor,
                    Use = RotSyringe.Throw,
                });

            var tools = new ToolRack("FruitLab:Tools")
                .Add(new Tool
                {
                    Label     = SutureTool.DisplayName,
                    Colour    = SutureTool.IconColor,
                    Use       = SutureTool.Click,
                    Equip     = SutureTool.Equip,
                    Unequip   = SutureTool.Unequip,
                    UsesWheel = SutureTool.Carrying,
                })
                .Add(new Tool
                {
                    Label   = VitalsMonitor.DisplayName,
                    Colour  = VitalsMonitor.IconColor,
                    Use     = VitalsMonitor.Click,
                    Equip   = VitalsMonitor.Equip,
                    Unequip = VitalsMonitor.Unequip,
                });

            _racks.Add(syringes);
            _racks.Add(tools);

            foreach (var rack in _racks) rack.Register();
        }

        public static void OnUpdate()
        {
            foreach (var rack in _racks) rack.OnUpdate();
        }

        public static void OnSceneReload()
        {
            foreach (var rack in _racks) rack.OnSceneReload();
        }
    }
}
