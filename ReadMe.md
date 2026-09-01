# FruitLab

![Version](https://img.shields.io/github/v/release/Luca-Nero/FruitLab?style=flat-square)
![Game Version](https://img.shields.io/badge/Game-v0.1%2B-blue?style=flat-square)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Donate-ff5e5b?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/Luca_Nero)

A surgery set for FRUKT. Throw a syringe and watch a regeneration wave close every wound limb by limb, put a body on life support that simply refuses to let it die, sew severed limbs back onto whoever you like, and read the whole thing off a bedside monitor that knows the patient by name.

---

## Features

- **Healing Syringe:** Flies with real physics, sweeps the full segment it covers each physics step, and sticks on contact with any limb - then goes kinematic and rides along with the host rigidbody as the ragdoll tumbles.

- **Lazarus Syringe:** Life support, in amber. Where the Healing Syringe puts a body back together, Lazarus keeps it *running* - and repairs nothing at all. Wounds stay open, missing limbs stay missing.
    
- **Suture Tool:** Pick a loose limb up, carry it, and click where it goes. Any limb onto any body, anywhere you click - the game supports grafting foreign limbs outright, and this uses that path.
    
- **Vitals Monitor:** A read-only panel that floats beside a body. Aim at a creature to read it; **left click** pins it so it keeps reading while you switch to something that does damage. Pin as many as you like.

- **Recall:** Press **X** to destroy every syringe in the world at once.

## Requirements & Compatibility

- **Prerequisites:** MelonLoader 0.7.2+ Installation. [Check out their Tutorial!](https://melonwiki.xyz/#/) and [FruitLib](https://github.com/Luca-Nero/FruitLib) **2.1.0** or newer in your `Mods/` folder.
- **Compatibility:** No known Incompatabilities.

## Installation

1. Download the latest release from the [Releases page](../../releases/latest).
2. Extract the archive.
3. Drop the contents into your game's `Mods/` directory.

## Controls (Defaults)

| Input | Action |
|-------|--------|
| Syringe slot + Left Click | Throw the selected syringe |
| Mouse Wheel | Cycle between the items in the held slot |
| Suture Tool + Left Click | Pick a loose limb up, then sew it where you are aiming |
| Right Mouse | Put a carried limb back down |
| Keypad 4 / Keypad 6 | Step back / forward through the six faces the limb joins by |
| Keypad 7 / Keypad 9 | Spin the limb anticlockwise / clockwise against the surface |
| Vitals Monitor + Left Click | Pin or unpin the panel you are aiming at |
| X | Recall - destroy every syringe in the world |

Slot numbers depend on what else is installed - the toolbar grows by one slot per registered mod item, and each slot draws its own key label.

## Configuration

`FruitLab.ini` is created next to the DLL on first launch. It is sectioned and documented - Controls, Healing Syringe, Lazarus Syringe, Suture Tool, Vitals Monitor and Debug - and is rewritten on load, so new fields appear on update while your existing values are preserved. The same settings are editable live through FruitLib's in-game menu.

Notable knobs: `StickRadius` (how close a throw has to pass to catch, in metres), `HealWaveSpeed` and `HealWorldSpeed` (the two halves of how the wave travels), `HealTickInterval` (the overall healing rate), `SpentLifetime` (how long used syringes linger; 0 keeps them forever), `LazarusDuration`, `SutureSeamOffset` (how far off the surface the seam is built - negative sinks the limb in), `SutureGlide` (how long a limb takes to travel to where you sewed it; 0 places it instantly), `SutureSettle` (the window of collision-damage immunity a fresh seam gets), and `VitalsRange`. Under Debug, `VitalsRaw` turns the monitor back into the diagnostic tool it started as, and `LogVitals` / `LogDiagnostics` print what an operation actually did, step by step.

`FruitLab.names.txt` is written beside the ini on first run. It holds two pools - given names and family names - which are paired up, so adding one line to either adds sixty more patients. Edit it freely; a scene reload picks up the changes. Turning naming off puts the raw object name back on the panel, which is the same for every body in the level.

## Known Issues

- **A decapitated body cannot be given its mind back.** The animation puppeteer leaves with the skull, and the game assembles a creature exactly once - there is no supported way to hand a driver to a different body. Sewing a head onto a headless torso restores ownership, vitals and physics, and the result stays inert. **The workaround is to carry the body and sew it onto the head instead:** the body then joins the head's creature and the whole thing wakes up. FruitLab says so in the log when it sees you doing it the other way round.

---

## Support & Feedback

Found a bug or have a suggestion? Feel free to open an issue on the [Issues page](../../issues) or catch me on Discord.

If you enjoy my work and want to support future updates, feel free to [buy me a coffee on Ko-fi](https://ko-fi.com/Luca_Nero)!

## License

[AGPL-3.0](LICENSE) © Luca Nero / Game Community
