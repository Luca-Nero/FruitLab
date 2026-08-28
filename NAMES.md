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
