# Obfuscated name map (game v0.1 → real names)

FruitLab targets the public v0.1 demo, which is obfuscated. `GameData/v0_12` …
`v0_14` are not, and line up member-for-member with v0.1. Everything below was
established by comparing those exports, not guessed from signatures.

`Native.cs` is the only file that touches an obfuscated member. Porting to a
later build should mean rewriting that file and nothing else.

## Types

| v0.1 | Real name |
|------|-----------|
| `bir` | `Effectors.EffectorReceiver` |
| `bit` | `Effectors.Types.Destruction` |
| `biu` | `Effectors.Types.Effector` |
| `biv` | `Effectors.Types.StabDamage` |
| `biw` | `Effectors.ReceiveMethods.IIndexEffectorSignalReceiver` |
| `bix` | `…Index.AbstractIndexEffectorSignalsHandler` |
| `bjb<T>` | `…Index.IndexEffectorSignalsHandler<TEffector>` |
| `bjd` | `…Index.IndexEffectorSignalsList` |
| `bje` | `…Index.IReadOnlyIndexEffectorFeedbacksHandler` |
| `ct` | `VoxelMeshGeneration.Tools.VoxelTools` |
| `cu` | `VoxelMeshGeneration.Tools.VoxelPositionData` |
| `hz<K,V>` | `TripledoseLibs…ReadOnlyMutantDictionary<TKey,TValue>` |
| `ii<T>` | `TripledoseLibs…ReadOnlyMutantHashSet<T>` |
| `bam` | `LVA.Creatures.AbstractCreature` |
| `baa` | `LVA.Limbs.EffectorsPerception.LimbEffectorPerceptionModule` |
| `rx` | `LVA.Organs.AbstractOrgan` |
| `zf` | `LVA.Limbs.Shape.IReadonlyLimbShapeDataHandler` |
| `zh` | `LVA.Limbs.Shape.LimbShapeDataHandler` |
| `zk` | `LVA.Limbs.References.LimbReferencesPublic` |
| `zs` | `LVA.Limbs.OrgansInteraction.IReadOnlyOrgansHandler` |
| `zt` | `LVA.Limbs.OrgansInteraction.MeshChangesSynchronizer` |
| `zw` | `LVA.Limbs.OrgansInteraction.OrgansSharedOperationsHandler` |

## Members

### `LimbEffectorReceiver`
```
wtb -> VoxelMesh            cyl -> TryGetFeedback<T>
cyn -> Receive<T>           cym -> TryReceive<T>
m_limbReferences : zk
```

### `zk` — `LimbReferencesPublic` (exact 1:1, 15 properties both versions)
```
xju -> AssignedCreature     xjv -> AssignedPuppeteer   xjw -> Limb
xjx -> Physics              xjy -> Mesh                xjz -> MeshSeparationModule
xka -> DismembermentModule  xkb -> Node                xkc -> ParentCoresSetter
xkd -> ParentCores          xke -> EffectorPerception  xkf -> OrgansHandler
xkg -> ShapeDataHandler     xkh -> AsChildLimbAttachIndexesCalculation
xki -> PlayerHoldInteractionEvents
```

### `zf` — `IReadonlyLimbShapeDataHandler`
```
xjm -> VoxelsCount          xjn -> VoxelsMap             xjo -> EnabledVoxelsIndexes
xjp -> EnabledVoxelsCount   xjq -> DisabledVoxelsIndexes xjr -> DisabledVoxelsCount
xjs -> AABBCenter           hua -> TryGetOrganByVoxelIndex
```
`xjq` / `xjr` are the destroyed voxels — the only damage readout reachable
without walking down to the organs' `EffectorCollector`s.

### `zw` — `OrgansSharedOperationsHandler`
```
xkl -> Organs               skc -> m_organsHandler
skd -> m_limbShapeDataHandler                skh -> m_meshChangesSynchronizer
ske -> m_limbEffectorPerceptionModule
skf -> m_onInitialEffectorsReceivedCalled
```

### `ct` — `VoxelTools`
The 13 real methods carry **sequential** names `diy`…`djk`; the other 11
overloads on the class are obfuscator decoys with random names (`mhj`, `nvb`,
`fmw`, `ltv`, `olk`, `mii`, `dzo`, `emm`, `hby`, `kzx`, `djj`'s twin). Declaration
order in v0.14 matches the name sequence:
```
diy -> GetEnabledVoxelIndexByDirection
diz -> GetEnabledVoxelPositionDataByDirection
dja -> PositionToVoxelIndex(mesh, position)                     ← clamped
djb -> PositionToVoxelIndex(mesh, pos, rot, scale, p, round)
djc -> VoxelIndexToWorldPositionMatrix
djd -> VoxelIndexToWorldPosition(mesh, index)
dje -> VoxelIndexToWorldPosition(pos, rot, scale, index)
djf -> VoxelIndexToWorldPosition(mesh, rotation, index)
djg -> VoxelIndexToWorldPositionWithoutRotation
djh -> PositionToVoxelIndexUnclamped(mesh, position)            ← NOT the one you want
dji -> PositionToVoxelIndexUnclampedWithoutRotation
djj -> PositionToVoxelIndexUnclamped(pos, rot, scale, p, round)
djk -> GetVoxelSizeOffset
```

### `LimbDismembermentModule` (real name in v0.1)
Reached from `zk.xka`. Members are sequential `hhq`…`hhw` / `rxr`…`rxz`, so they
are the real ones. Matched to v0.14 by signature:
```
hhq -> Construct(IAbstractLimbsFactory, IAbstractCreatureFactory)
hhr -> add_OnDismember     hhs -> remove_OnDismember
hht -> Initialize(PrefabID, EntityInternalCollectionsHandler<…>, VoxelMesh,
                  LimbPhysics, ICreatureHandler, int)
hhu -> GetAncestorVoxelIndex()          hhv -> GetAncestorVoxelIndexPosition()
hhw -> CreateNewLimbsFromNewMeshesData(IReadOnlyList<SeparatedMeshData>,
                                       IReadOnlyCollection<int3>)
rxr -> OnDismember (the delegate field behind hhr/hhs)
```
`hht`'s parameter list is an exact match; `hhq` takes two arguments where v0.14
takes three, because v0.14 added `IVoxelMeshPaintService` (and the matching
`m_surfacePaintService` field, absent in v0.1).

`hhw` carries `CallerCount(0)` but a 115-wide xref span: it is registered as
`SeparationPerformer`'s `m_customCreateNewMeshesMethod` and only ever invoked
through that delegate. Harmony still catches it — the detour is on the native
method, which is where the delegate lands.

**Dismemberment is mesh separation, and it replaces limbs rather than modifying
them.** When destruction disconnects a group of voxels, `SeparationPerformer`
splits the mesh and `hhw` builds a *brand new limb* per group out of the limb
prefab. `SeparatedMeshData` carries voxel indexes (`enabledVoxelsIntIndexes`,
`enabledVoxelsVectorIndexes`, `m_ancestorIndex`) and nothing else — no colours —
so a severed piece comes out as untouched flesh under a `LimbEffectorReceiver`
nothing has ever seen. Anything a mod is doing to a limb therefore *vanishes* the
moment that limb is severed, and looks like the flesh healing itself. Follow it
by hooking `hhw` and adopting the pieces (see `Dismemberment.cs`).

### `rx.gqz` -> `AbstractOrgan.DestroyLVA()`
`AbstractOrgan` has nine public no-argument `void` methods in v0.1 and only
three in v0.12, so position matching fails — six are decoys. Identified from the
interop metadata instead: `gqz` has `CallerCount(1)` and 7 outgoing xrefs,
matching v0.12's `DestroyLVA` exactly; `gqy` has `CallerCount(1)` and 2, matching
`Load`. Every decoy has `CallerCount(0)`.

### `VoxelMesh.Voxels`
```
wua -> Count   wub -> Size   dfc -> SetVoxel
length / height / width keep their real names
dfa -> IsIndexOutOfRange(Vector3Int)   dfb -> IsIndexOutOfRange(int3)
```
Note the polarity: `dfa`/`dfb` return **true when out of range**. They are a
usable bounds check, just inverted from how they read.

## Obfuscator tells

`Il2CppGUPS.Obfuscator` is in the v0.1 build. Two heuristics that held up
everywhere here:

1. **Real members get sequential names within a type; decoys get random ones.**
2. **Decoys have `CallerCount(0)`.** Where names are shuffled (`rx`, `baa`, `zw`),
   `CallerCount` plus the xref span width identifies a method reliably — the xref
   count is the number of calls the body makes, and it survives obfuscation.

## LVA parameters (added for the Lazarus Syringe)

| v0.1 | Real name |
|------|-----------|
| `bcx` | `LVA.Core.LVAEntity` (a MonoBehaviour; creature, limb and organ all are one) |
| `bcz<T,S>` | `LVA.Core.LVAEntityInitializer<TParameter,TSystem>` |
| `g` | `Core.ManagedBehaviour` · `bgb` = `RegistrableObject` |
| `bcg` | `Core.IInternalParametersPublicAPI` |
| `bci` | `Core.IInternalSystemsModulePublicAPI` |
| `bca` | `Core.IEffectorsProcessingHandler` |
| `bjq` | `Data.CustomTypes.LimitedValue` |
| `bjs` | the readonly parameter/limited-value wrapper |
| `bdc` | `Core.LVAParameter` · `bdb` = `LVAInternalParameter` |
| `baq` | `Creatures.CreatureInternalParameter` · `zq` = `Limbs.LimbInternalParameter` |
| `bbc` | `Creatures.Systems.BloodTank` · `bbd` = `IBloodTankInteraction` |
| `bba` | `Creatures.Systems.BloodDrainSoundHandler` · `bbb` = its interaction |
| `bbl` | `Creatures.Parameters.UsedBloodAmount` |

### `bcx` — `LVAEntity`
The seven interface-typed properties carry sequential names, so declaration order
in v0.14 maps them directly:
```
xlg -> ExternalDependeciesPublic        xlh -> EntityInternalParametersPublic
xli -> EntityExternalParametersPublic   xlj -> EntityInternalSystemsPublic
xlk -> EffectorsProcessingHandler       xll -> InternalDependeciesProtected
xlm -> InternalSystemsProtected
squ -> EntityType   sqv -> LVADestroyed   sqw -> LaunchPipeline
```
`xlk : bca` independently confirms `bca` = `IEffectorsProcessingHandler`, which was
already reached from the other direction via `baa.skr`.

### Reaching parameters: use the module, not the public API

`bcx.sqq` is `m_internalParameters`, a concrete `InternalParametersModule`
(`bcx.bcr`) holding `spv : Dictionary<Type, LVAInternalParameter>`. Walk that.

**The public API route does not work.** `bcx.xlh` resolves fine, but its lookups
(`iep<T>` / `ieq<T>`) are *generic methods on an IL2CPP interface*, and
generic-instance virtual dispatch through one silently resolves nothing here —
confirmed in-game 2026-08-29, 0 of 34 parameters returned on a creature that plainly
had them, in every state tested (pristine, dead, freshly spawned). No exception, just
`false`. Treat generic interface methods on this build as unusable.

The dictionary route is better on every axis: no generic dispatch, no cast back from
the readonly wrapper (`bdb` already derives from `bjq`, so a parameter *is* its value
object), no hardcoded type list, and it picks up the parameters later versions added
for free. The `Type` key also gives a real name to log.

`bcx` field order — sequential `sqm`…`sqt`, matching v0.14 exactly, with the trailing
`bool m_initialized` as the anchor:
```
sqm -> m_updateLoopService    sqn -> m_injector
sqo -> m_internalDependecies  sqp -> m_externalDependecies
sqq -> m_internalParameters   sqr -> m_externalParameters
sqs -> m_internalSystems      sqt -> m_initialized
```

`bcx.bcr` — `InternalParametersModule`:
```
spv -> m_parameters : Dictionary<Type, LVAInternalParameter>
spu -> m_assignedEntity
ift(Type, out bdb) -> TryGetInternalParameter(Type, ...)   ← non-generic, usable
ifs<T>(out bdb)    -> TryGetInternalParameter<T>
ieo() -> GetAll     ifu<T>(T) -> AddInternalParameter<T>
```
It also has MuteAll/UnmuteAll among `hrb` / `ifv` / `za` (two real, one decoy —
`ifv` sits in the real `if*` block). Untried; the fallback if the dependency solver
out-fights a per-tick restore.

### `bcg` / `bci` — the module APIs
```
bcg.ieo()      -> GetAll() : IReadOnlyCollection<IReadOnlyLVAParameter>
bcg.iep<T>(out) -> TryGetReadonlyInternalParameter<T>
bcg.ieq<T>()   -> GetReadonlyInternalParameter<T>
bci.ies<T>(out) -> TryGetSystemExternalInteraction<T>
bci.iet<T>(bool) -> GetSystemExternalInteraction<T>
```
`GetAll` is awkward to use: an Il2Cpp `IReadOnlyCollection` has no indexer and its
enumerator supports neither `foreach` nor a cast to the BCL interface. Ask for
parameters by type instead.

### `bjq` — `LimitedValue`
**In v0.1 a parameter still derives from LimitedValue** (`bdb : bdc : bjq`), so the
value lives on the parameter itself. v0.12+ refactored this to composition —
`LVAParameter.Inner` — so a port has to insert that hop.

Field order pins the mapping, with the static `TOLERANCE` as the anchor:
```
tcq -> minValue    tcr -> initialValue    tcs -> m_value    tct -> TOLERANCE (static)
tcu -> MaxValue    tcv -> PercentageProgress
tcw -> PercentageValue    tcx -> MinMaxDelta
jdl -> SetValue(float)    jdn -> SetMax(float, float?)
jdp / jdq -> IsMin() / IsMax()
jcw jcx jcy jcz -> add/remove OnChange, add/remove OnChangeMax
```
`jdl` is the only `void(float)` in the sequential `jdl`…`jdt` block, and it is the
same `bjq.jdl(float)` that appears in the death cascade — two independent signals.

**`tcr` is `initialValue` as in the constructor seed, NOT the spawn state.** Measured
in-game 2026-08-29: on a pristine creature `initialValue` reads 0 for most parameters
while the live value sits at max — LVA raises them during initialisation. Restoring to
it writes zero into everything and kills the body instantly. Use `tcu` (MaxValue): on a
healthy creature in v0.1 every parameter sits at or just under its max.

### Creature parameters — all four identified 2026-08-29

Confirmed by watching a single chest wound play out on the Vitals Monitor, not by
name matching:

| key | parameter | behaviour that identifies it |
|-----|-----------|------------------------------|
| `bbl` | `UsedBloodAmount` | `BloodTank.snn` points at it; keeps draining after death |
| `bbj` | `CognitionLevel` | the kill registers the instant it reaches 0 — this alone is death |
| `bbh` | `Balance` | tracks the worst limb value almost exactly; at 0 the body cannot stand |
| `bbk` | `GeneralMuscleForce` | snaps 100 → 5 → 100 in time with each attempt to get up |

`CreatureTotalPain` is the one v0.14 has that v0.1 does not.

**Death is not bleeding out.** In the observed run consciousness hit 0 with blood still
at 55%, and blood carried on draining afterwards. Blood loss drives consciousness down,
but the kill is registered off `CognitionLevel` alone.

**Balance is clamped by the limbs.** Creature `bbh` and the worst limb value converge on
each other continuously — the limbs appear to cap it.

Older measurement, healthy vs. shot dead (`bbk` is the odd one, 100 in both, because
muscle force is intrinsic capacity rather than something death zeroes):
```
             healthy      dead
bbh / bbj    ~100         0        creature-level, these are what flatline
bbl (blood)  5146/5146    4780/5006   ~95% even on a corpse — blood is NOT the limiter
zr (limb)    ~100         0        uniform across limbs
xr (limb)    ~100         50 / 21.5 / 100   varies per limb with local damage
```
Blood being near-full on a dead body is the useful surprise: what actually stops a
creature functioning is `bbh`/`bbj` and the limbs' `zr` going to zero.

### Parameter types present in v0.1
Creature (`: baq`): `bbh`, `bbj`, `bbk`, `bbl`. Limb (`: zq`): `xr`, `zr`.
v0.14 has five creature and six limb parameters, so the extras arrived after v0.1 —
expect this list to grow on the port. Only `bbl` is individually identified
(`BloodTank.snn` points at it), and FruitLab does not need the others identified:
it restores all of them to initial.

### `bbd` — `IBloodTankInteraction`
Sequential block `ibm ibn ibo ibp` ↔ `Drain, Fill, ExpandCapacity, ShrinkCapacity`
in v0.14 declaration order; the other six `void(float)` methods on it are decoys.
**Unused on purpose** — the order is inferred rather than proven, and mistaking Drain
for Fill would bleed a body dry. Writing `initialValue` through `bjq.jdl` needs no
such guess.

## What decides a creature is dead

`sf` = **`LVA.Organs.Variants.CrunchedDeathCounter`** (an `OrganSystem`, so it is not a
component and cannot be reached with GetComponent — go through the organ's systems module).
1:1 with v0.14:

```
gsx -> TryRegisterDeath()          gsw -> FUCKINGCRUTCHREGISTER() : IEnumerator  [sic]
gst(gd) -> Construct(...)          gd  -> IKillsService
rqt -> m_cognitionLevel : IReadOnlyLVAParameter    ← the life/death signal
rqu -> m_registered : bool                         ← one-shot latch
rqv -> m_killsService
```

**Death is not a flag — it is `CognitionLevel` bottoming out.** `CrunchedDeathCounter` watches
that one parameter and calls `TryRegisterDeath` when it does, which reports to `IKillsService`
(what the pause-screen `KillsCounter` view displays) and sets `m_registered` so one corpse cannot
be counted twice. That latch is why a revived creature killed a second time does not increment
the counter, and `gsx` is the frame in the NRE stack from
[[frukt-hud-deactivation-breaks-lva]] — the counter is downstream of the vitals graph, which is
why deactivating its UI broke death handling.

Also confirms `bjs` = `IReadOnlyLVAParameter`, reached independently from `bcg.ieo()`.

`tx` = `OrganEffectorPerceptionModule` (`guy(bool)` = `ApplyFeedbacks`, `guw()` =
`GetReceivedEffectorsTypes`, `rsh`…`rsn` = its fields), and `ua<T>` = `EffectorCollector<T>`
(`gvn(int)` = `UpdateOverallProgress`), `uc` = `IEffectorCollector`. That whole stack is the
damage path: apply feedback → collector progress → `LimitedValue.SetValue` → solve → observers.
FruitLab's freeze cuts it at SetValue.

## Inputs vs outputs: why a write does not stick

`LimitedValue.SetValue` writes the *output* of the dependency graph. The solver keeps
its own inputs and re-emits them on the next solve, so any internal parameter you write
reverts the moment something disturbs the creature. Measured 2026-08-29: a body whose
vitals had been forced to 100% snapped back to **44% blood** when a leg was shot — blood
is a tank derived from nothing, so a recomputed value could not have produced that
number. It was read back from an input.

The inputs are the entity's **external** parameters:
```
bcx.sqr  -> m_externalParameters : ExternalParametersModule (bcx.bcs)
bcx.bcs.spw -> m_externalParameters : Dictionary<Type, ExternalParameterInfo>
bcx.bct  -> LVAEntity.ExternalParameterInfo
bcx.bct.spz -> externalParameter : LVAExternalParameter   (bda)
bda : bdc : bjq — so an external parameter takes the same SetValue as everything else
```
`bda` = `LVAExternalParameter`. `bbi` (held by `BloodTank.sno`) is one of these, and is
the blood figure the internal `bbl` is derived from.

**Rule of thumb:** write an internal parameter for a temporary effect (Lazarus holds them
and blocks everyone else's writes); write the external parameter for a permanent one.

### External parameters observed on a human (v0.1)

v0.14 has exactly five `LVAExternalParameter` subclasses, which names all five:

| v0.1 | real name | notes |
|------|-----------|-------|
| `bbi` | (the blood figure) | 0..5146.21, the input behind internal `bbl` |
| `ri` | `PuppeteerToCreatureGeneralMuscleForce` | written every frame by the puppeteer |
| `rh` | `PuppeteerToCreatureBalance` | written every frame by the puppeteer |
| `bbs` | `CognitionActivityToggle` | 0..1, sits at 1 |
| `xq` | `Limbs.Systems.MuscleForceTension` | 0..100, per limb, the input behind limb `xr` |
| `yp` | `Limbs.Systems.BalanceAgentSystemValue` | 0..1, sits at 1 |

**`rh`, `ri` and `xq` are live puppeteer output, not damage state.** Restoring one is
meaningless — the animation system rewrites it within the frame and everything derived
from it follows. Writing them is what left a healed body with muscle, balance, both limb
values and every `xq` sitting on the *same* arbitrary number (61.962 in one run): that
figure was just the puppeteer's blend at the instant it took the values back.

A permanent restore must skip them. Lazarus deliberately does not — overriding the
puppeteer is precisely how it holds a ruined body upright.

**Externals are sparse and added on demand.** `TryAddExternalParameter` means a limb only
carries an `xq` if something has influenced it — on the body measured, `xq` existed on the
pelvis and legs (the damaged half) and nowhere else, and the spine and head carried no
externals at all. Never assume a given entity has a given external.

`rh` and `ri` are written continuously by the puppeteer, so restoring them is a no-op that
gets overwritten within the frame. Harmless, and correct — muscle tension should be driven.

Limb `zr` has **no external at all**, so it is derived purely from organ/voxel state. That is
why it is the last thing to come right after a heal: it follows the effector collectors, which
keep moving for several frames after the voxels are rebuilt.

## Death's one-way switches (outside the LVA graph)

Not everything death does is a parameter. `FootFrictionControl` — a **MonoBehaviour**, and
in v0.1 it keeps its real name under `Il2CppActiveRagdoll.Scripts` (v0.14 moved it to
`LVA.Puppeteers.Variants.Humanoid`) — swaps both feet to a zero-friction physics material
when consciousness reaches zero, so a corpse slides instead of catching on the ground.

**There is no counterpart that swaps it back.** The game has no notion of getting up again,
so a revived body walks on ice: the legs step, the feet skate, it goes nowhere. Call
`SetDefaultFriction()` on revival; the walk cycle's alternating SetLeft/SetRight resumes
from there.

```
SetDefaultFriction / SetLeftFriction / SetRightFriction   real names, all three
m_zeroFriction / m_defaultFriction / m_isNeeded           real names
ddt -> DisableOnZeroCognitionLevel()
dds(bam, qx) -> Initialize(AbstractCreature, HumanoidCreatureLimbsProvider)
php / phq -> m_leftFootPhysics / m_rightFootPhysics
phr -> m_assignedCreature   phs -> m_cognitionLevel (bjs)
pht / phu -> the foot listeners (bav = INativeLimbListener)
phv / phw -> m_rightFootDestroyed / m_leftFootDestroyed
```

**Expect more of these.** Anywhere the game reacts to death it is likely one-directional,
because nothing was ever meant to come back. When a revived creature behaves oddly in a way
the vitals do not explain, look for a `…OnZeroCognitionLevel`-shaped method rather than a
parameter.

## Posture is not health

The single most misleading thing in this system. Lift a **pristine, undamaged** ragdoll
off the ground with the cursor and read it:

```
muscle (bbk)  5%     balance (bbh)  0%     limb zr  5%     limb xr  22%
rh 0%         ri 5%         xq 22%
blood 100%    consciousness 100%    organs 100%
```

Those are the figures a healthy body reports while dangling, and they are the same ones
that appear on a corpse. **`rh ri xq bbk bbh xr zr` describe how a body is carrying
itself** — ground contact, muscle tension, poise — not how hurt it is. A body in mid-air
is carrying itself not at all.

Consequences:

- **Never restore them.** There is no correct value to write; it depends on what the body
  is doing this instant and the puppeteer rewrites it continuously. Forcing them to max
  made a healed body read 100% on values a healthy one only reaches when standing squarely
  on both feet.
- **Never colour them as health,** and never let them feed a life/death readout. Balance
  at 0 is a body off its feet, not a body in trouble.
- **`xr` at ~22% on a leg is normal**, not damage. So is `zr` at 5%.

What genuinely tracks health: `bbj` consciousness, `bbl`/`bbi` blood, and the organ
parameters `tl` `tm` `ud`. That is the whole list.

**The diagnostic that settles this class of question: read a pristine body first.** Every
figure above had been rationalised as injury for several sessions purely because a healthy
baseline had never been taken.

## Voxel colour only becomes visible where topology changed

`Voxels.dfc` writes the colour and `VoxelMesh.dgq` returns cleanly, but a chunk whose
voxels merely changed *colour* is not re-meshed — only one whose enabled-state changed
is. So painted-but-intact flesh keeps its old surface indefinitely, and the new colour
appears only when something near it is destroyed and the chunk gets rebuilt for that.

This is why BloodColor works and looks like it proves otherwise: it recolours interior
flesh (`R−G ≥ 60` excludes skin), which is invisible until a cut exposes it — and a cut
is a topology change. It never had to make a colour change visible on undisturbed skin.

**To show a colour change on intact flesh, something in that chunk has to be destroyed.**
FruitLab's rot eats a hashed scattering of the discoloured front (`RotPitting`) for
exactly this reason; the pitting is also, conveniently, what rotting flesh looks like.

**`dgq` is the only way to make it visible.** Destroying a voxel does make the game
re-mesh that chunk, but from its own *changed-data map* — only voxels the game itself
altered — so a colour a mod wrote into the array is not picked up. Painting without `dgq`
shows nothing at all. It throws a duplicate-key from `dhh` (Show) every time; swallow and
throttle it, but do not remove it.
