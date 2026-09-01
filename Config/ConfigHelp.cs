using System.Collections.Generic;

namespace FruitLab
{
    internal static class ConfigHelp
    {
        public static string Mod      => "FruitLab";
        public static string Version  => Core.Version;
        public static string FileName => "FruitLab.ini";

        public static readonly Dictionary<string, string> Help = new Dictionary<string, string>
        {
            ["RecallKey"] = "key to destroy every syringe in the world",

            ["ThrowSpeed"]       = "initial throw velocity (m/s)",
            ["StickRadius"]      = "how close to a limb the syringe has to pass to stick, in metres — raise it if throws slip past, lower it for a needle that has to be aimed",
            ["SpentLifetime"]    = "seconds a spent syringe lies on the ground before despawning (0 = never)",
            ["HealTickInterval"] = "seconds between heal ticks — the overall healing rate",
            ["HealSignal"]       = "heal strength written into each voxel; the default is well past what any wound can undo",
            ["HealWaveSpeed"]    = "voxel-units the wave front expands per tick within one limb — low is a slow crawl, high is a sharp pop",
            ["HealWorldSpeed"]   = "metres/sec the wave front travels between limbs, which sets the stagger",

            ["LazarusDuration"] = "seconds a Lazarus dose keeps a body alive before it runs out",
            ["LazarusInterval"] = "seconds between vitals top-ups while a dose is running — lower reacts faster, higher costs less",

            ["RotSeamReach"]     = "how close the rot has to get to a joint, in voxels, before it crosses into the next limb. Joint anchors sit outside the flesh they hold and the hips are the worst offenders, so too small a value leaves legs untouched on a body that has rotted away around them",
            ["RotEnabled"]       = "put the Flesh Rot syringe back on the toolbar. Shelved on the v0.1 demo: it spreads and looks right, but a limb it eats through leaves unowned, near-invisible fragments behind that nothing cleans up. Off, it costs nothing at all",
            ["RotSpreadSeconds"] = "seconds the rot takes to cross a joint into the next limb",
            ["RotBlackenAfter"]  = "seconds a voxel stays rot-purple before it turns black",
            ["RotDestroyAfter"]  = "seconds a voxel stays black before it is destroyed",
            ["RotTickInterval"]  = "seconds between rot ticks",
            ["RotDamage"]        = "destruction dealt per rotted voxel; high enough that flesh actually goes",
            ["RotMinPiece"]      = "voxels a severed piece must still have before the rot bothers with it, and the point at which a limb counts as finished. A sphere eating through a pelvis shatters it, and every shard that gets its own infection shatters again — this is what stops the run ending in a scatter of near-invisible crumbs",
            ["RotMarkDamage"]    = "damage the leading discoloured front deals — 1 just registers the rot with the body without taking anything, raise it to make the creature suffer as it spreads, 0 disables it",

            ["SutureAimRange"]   = "how far you can be from a limb and still click it",
            ["SutureTurnStep"]      = "degrees the limb spins per key press against the surface it is joining. 90 gives four quarter-turns per face, which with six faces reaches every placement; lower it for free-angle work",
            ["SutureGlide"]         = "seconds a limb takes to travel from where it is lying to where you sewed it. It is there to read as an operation rather than a teleport; 0 places it instantly",
            ["SutureKeyFacePrev"]   = "step back through the six faces the limb can join by (keypad 4)",
            ["SutureKeyFaceNext"]   = "step forward through the six faces the limb can join by (keypad 6)",
            ["SutureKeySpinLeft"]   = "spin the limb anticlockwise against the surface (keypad 7)",
            ["SutureKeySpinRight"]  = "spin the limb clockwise against the surface (keypad 9)",
            ["SutureSeamOffset"]    = "how far off the surface the seam is built, in metres; the game's own limb joints sit set back from where two limbs meet rather than on the skin, and clicking a surface gives a point that is too deep. Negative sinks the limb into the body instead",
            ["SutureSeamCollision"]  = "let the sutured limb collide with the body it was sewn to. Leave this off. Two limbs meeting at a joint overlap by design \u2014 that is what a seam is \u2014 so the limb at the join grinds against its new neighbour forever, and the game reads that as impact damage: a sphere of destroyed flesh at the seam, which disconnects the limb and severs it again. Only that one limb is excused; anything hanging off it still collides normally",
            ["SutureBreakForce"] = "force needed to tear a sutured limb off again; 0 makes the seam unbreakable",
            ["SutureSettle"]     = "seconds of collision-damage immunity a freshly sutured limb and whatever it touches get; without it the limb becoming solid inside a chest punches a hole in both, and the game severs it again where the flesh went. 0 disables",
            ["SutureGhost"]      = "show a hologram of the limb where it would attach while you carry it",
            ["SutureNative"]     = "when a limb is still one of the body's own, put it back in its actual socket rather than where you clicked. That slot is where its node tag lives, and the puppeteer only knows limbs by tag, so it is the one route by which a reattached limb can end up animated instead of hanging. Off means every limb goes exactly where you put it",
            ["SutureAdopt"]      = "hand the sutured limb over to the body it was sewn to, so it shares that body's vitals and gets animated instead of hanging. Without it a reattached limb sits in the right slot while still belonging to a creature of its own. The whole assembly is handed over, not just the limb at the seam, or a reattached arm ends up with a forearm and hand that still belong elsewhere",
            ["SutureGraft"]      = "also join the two in the creature hierarchy, not just physically — off gives you a limb that hangs correctly but that the body does not know it has",
            ["SutureAlign"]      = "lay the limb flush against the surface you attach it to, so the face you are presenting meets the body squarely. Off leaves it at whatever angle you are holding it",
            ["VitalsRaw"]        = "list every parameter under the readout — organs, limbs, inputs and posture, with their raw keys. Off, the panel shows consciousness, blood and a pulse, which is what you want while playing; on, it is the diagnostic tool it started as",
            ["VitalsRange"] = "how far the Vitals Monitor reaches when you aim at a body, in metres",
            ["SyringeModel"]      = "throw the modelled syringe instead of the coloured box it started as. Off falls back to the box, which is also what happens on its own if the meshes cannot be loaded",
            ["SyringeScale"]      = "size multiplier for the syringe model. 1 is the size it was modelled at, roughly 23cm from thumb rest to needle tip",
            ["SyringeGlassAlpha"] = "how see-through the barrel is, 0 to 1. The .mtl exports opaque unless you turn the material's alpha down in Blender before exporting, so this overrides it — the barrel is meant to show what is inside",
            ["SyringePlungerDraw"] = "how far the plunger sits pulled out on a full syringe, in metres along its long axis. Negative draws it away from the needle, which is the direction that reads as full - the converter negates Z on the way in from Blender, so this is the opposite sign to what the modelling file shows. Zero holds the plunger down and turns the animation off",
            ["VitalsBpmRestMin"] = "lowest resting heart rate a patient can be born with. Each body draws its own from this range and keeps it; blood loss then climbs from there. 60-100 is the clinical normal, but most people sit in the low seventies, so the default band is narrower than the textbook one",
            ["VitalsBpmRestMax"] = "highest resting heart rate a patient can be born with. Set it equal to VitalsBpmRestMin to give everybody the same baseline again",
            ["VitalsSpecialOdds"] = "one body in this many draws a name from the [special] list in FruitLab.names.txt instead of the ordinary two. Set it to 1 to see them every time, or 0 to turn them off entirely",
            ["VitalsNames"] = "give each body a name, drawn from FruitLab.names.txt beside this file and kept for as long as that body exists. Off, the panel shows the prefab name instead, which is the same for everyone in the level. Edit the list to add your own; a scene reload picks up the changes",

            ["LogDiagnostics"]   = "log what an operation actually does, step by step, with the voxels each step destroys and in which limb; the answer to \"why did that leave a hole\" lives here",
            ["DiagWindow"]       = "seconds to keep reporting after an operation finishes, so anything the physics does a moment later still shows up",
            ["LogVitals"] = "log each creature's LVA vitals on impact and on expiry; turn on if a body will not stay alive and you need to see which value the game is dragging back down",
        };
    }
}
