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

Measured values, healthy vs. shot dead (creature `bbk` is the odd one, 100 in both):
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
