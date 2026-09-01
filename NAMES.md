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

### `VoxelMesh` — making a colour change visible
```
dgq -> CreateMesh(Voxels, Vector3 displacement, bool containsInitialEnabledVoxels)
dgr -> StartStepwiseMeshChanging(int)     dgs -> WriteStepwiseMeshChange(VoxelMorphData)
dgt -> CompleteStepwiseMeshChanging(int)  dgv -> Hide()        dgw -> Show()
dgx -> CheckoutUpdateChunkRequest(Action onComplete)
dgy -> CheckoutUpdateChunkRequest(MutantDictionary<int3, Voxel>, int,
                                  Action<…CallbackData>, Action<…CallbackData>)
dgz -> RegisterUpdateChunkRequest(UpdateChunksRequest)   dha -> IterateUpdateRequestsQueue()
```
Positional against v0.14's declaration order, with `dgq`, `dgr`, `dgt`, `dgy` and
`dgz` all confirmed on signature — `dgy`'s four-argument shape pins the Checkout
pair, and `er` = `UpdateChunksRequest` pins `dgz`.

**`dgx` is what you want, not `dgq`.** `dgq` is not a re-mesh, it is a *build*: it
recreates the chunk set and re-adds every chunk to a dictionary that already holds
them, which is the long-standing duplicate-key `ArgumentException` on
`int3(0, 0, 0)`. It happened to work only because the geometry updates before the
throw. `dgx` queues an update request the way the game does.

**Correction:** an earlier note here identified `dhh` as `Show`. It is not — `dgw`
is `Show`. `dhh` is the private chunk-creation step *inside* `dgq`, which is why
it appears above `dgq` in the stack trace of that exception, and why the key it
duplicates is a chunk index.

Consequence worth knowing: with a real update path available, painting no longer
depends on destroying a voxel nearby to force a re-mesh. That constraint is what
the rot syringe's "pitting" existed to work around.

### Limb ownership is not node membership
A limb can sit in the correct slot of the correct creature's node hierarchy and
still belong to a creature of its own. Severing gives it one (see the
dismemberment notes), and **nothing in the hierarchy API takes that away**:
`AttachChildAsNative`, `AttachNodeAsThirdparty` and
`CreatureHierarchyOperationsModule.TryAddAsNative` all leave `AssignedCreature`
pointing at the old owner. In game that is a limb that hangs off the body,
carries its own blood and vitals, and is animated by nothing.

#### The puppeteer cannot be moved between bodies
`qb` is `IAbstractPuppeteer` in v0.1 (five abstract members; the `Initialized` /
`MarkInitialized` pair lives on the concrete class).
```
ggr -> References    ggs -> Initialize(AbstractCreature)
ggt -> GetLimb(AbstractNodeTag)   ggu/ggv -> add/remove OnAnimationDone
```
`bam.hza` -> `AbstractCreature.AssignPuppeteer(AbstractPuppeteer)` (protected, but
Il2CppInterop widens it; the only protected `void(qb)` inside bam's run
`hya`…`hzc`, the other four being decoys), guarded by `bam.smb`
(`HasAssignedPuppeteer`).

**The handshake has two halves and only one can be replayed.** Assigning works.
Re-targeting does not:
```
SystemException: Double initialization is a sign that you've taken a shit
somewhere. Exception sender: HumanoidPuppeteer
```
Assigning without re-targeting is *worse* than doing nothing: the body is driven
toward the pose and facing of the creature the rig was built for — in game, a
torso locked into a cower that snaps back when you try to turn it. FruitLab
therefore calls neither.

**The puppeteer travels with the head.** Decapitate a creature and the driver
leaves with the skull, which is why a headless body keeps its blood and its
consciousness (`bbj` reads ~95% with no head at all) and still does nothing.
Reattaching a head restores ownership, vitals and physics, but not the mind.

**The way round it is to reverse the operation.** Ownership flows from the carried
half *into* the target's creature, so carry the **body** and sew it onto the
**head**: the body's limbs are adopted into the head's creature, which already has
a correctly initialised puppeteer, and nothing needs re-targeting. Confirmed in
game — the result goes through the proper stages of getting up, curling and
spreading out. The pelvis enters via `AttachParentAsNative`, which is exactly what
that method is for: making a node the new root of a hierarchy.

So the rule is: **whichever half holds the mind must be the target, not the thing
you carry.**

**Correction.** An earlier note here claimed cognition reads high on a headless
body *because* the brain is absent. That is wrong: observed directly on the Vitals
Monitor, a decapitated body reads `bbj` 0% and DECEASED. Cognition does track the
head.

What is still true is narrower and still unexplained: during the suture work,
`bbj` read 94–95% on a body that had been headless for a minute, both before and
after a head was sewn on. Something about that measurement or that body differed —
possibly the creature being read, possibly prior healing — and it was never run
down. Treat `bbj` as a real consciousness readout; treat the suture-time reads as
unexplained rather than as evidence about how cognition works.

Headshots not mattering on a body with a *reattached* head is therefore a gap in
what reattachment wires up, not a general property of cognition.

#### `bcl` — `IParentCoresSetter` (this is where ownership lives)
Reached as `zk.xkc` (`LimbReferencesPublic.ParentCoresSetter`). An interface, so no
decoys, and v0.1's four members line up with v0.14's declaration order position
for position — two are unambiguous on signature alone as well.
```
idu -> SetCreature(AbstractCreature)      idv -> InstallToAssignedCreature()
idw -> RemoveCreature()                   idx -> AssignPuppeteer(AbstractPuppeteer)
```
From the Ghidra decompile of `NativeLimbAttachModule.TryAddDetachedLimb`, the game
hands a **severed** limb over like this — the whole detached sub-tree, in two
separate passes, then the node attach:
```csharp
allNodes = node.hierarchyNavigator.GetAllNodes();
allNodes.ForEach(n => n.assignedLimb.References.ParentCoresSetter.SetCreature(creature));
allNodes.ForEach(n => n.assignedLimb.References.ParentCoresSetter.InstallToAssignedCreature());
asParent ? AttachParentAsNative(node) : AttachChildAsNative(node);
```
Both passes complete before the other begins, and there is **no LaunchLVA** on this
path. Ownership is per limb, so the whole group has to move together.

`AbstractLimb.hgj` -> `LaunchLVA(AbstractCreature)` also moves ownership, but it is
the path for a limb that was *never initialised* (`TryAddNonInitializedLimb`, which
pairs it with `ParentCoresSetter.SetCreature` and no node walk). On a limb whose LVA
is already up it re-enters module initialisation and trips a double-call guard —
which is why the two-pass route above is the one to use. It is the sole
class-level `void(bam)` on `AbstractLimb`, so there is no ambiguity — the other
eight candidates sit on the nested `AbstractLimb.wv` (`LimbSystemCore`), where
`OnAssignCreatureCore` / `OnRemoveCreatureCore` live and are per-system
notifications, not levers.

**It throws, and it works anyway.** On a limb whose LVA is already running:
```
Double call of this method is not allowed. / Sender type : LVA.Limbs.Variants.Arm
  at bcx+bcp.ife ()
  at LVA.Limbs.References.LimbReferencesPrivate.hvi (bam a, …)
  at LVA.Limbs.AbstractLimb.hgj (bam a)
```
The references — creature *and* puppeteer — are assigned inside `hvi` before
module initialisation is re-run and hits its double-call guard, so the exception
is the tail of the call, not the whole of it. **Judge it by reading
`AssignedCreature` back, never by whether it threw**; reporting the throw as a
failure hid a working adoption behind a stack trace for a round.

Ownership is per limb, so a severed *assembly* needs every limb relaunched, not
just the one at the seam.

### Limb hierarchy — attach and detach
A limb's place on a body is a node in the creature's hierarchy. Attaching and
detaching are operations on that hierarchy; the joints and transforms follow.

| v0.1 | Real name |
|------|-----------|
| `vd` | `LVA.NodesHierarchy.Nodes.LimbNode` |
| `vj` | `…Nodes.INodeSystem` |
| `bbn` | `LVA.Creatures.Hierarchy.CreatureHierarchyOperationsModule` |
| `bbo` | `…Hierarchy.LimbDetachModule` |
| `bbp` | `…Hierarchy.LimbsHierarchyHandler` |
| `bbq` | `…Hierarchy.NativeLimbAttachModule` |
| `bio` | `IAbstractCreatureFactory` |

```
bam.smd  -> AbstractCreature.LimbsHierarchyHandler   (getter bam.hyo)
bbp.soe  -> LimbsHierarchyHandler.Operations         (getter bbp.ici)
bbn.ibx  -> CreatureHierarchyOperationsModule.TryAddAsNative(AbstractLimb)
bbn.iby  ->   …TryDetach(AbstractLimb, AbstractCreature)   ← the context menu's
bbn.ibz  ->   …DetachChildren(AbstractLimb)                  Detach Limb
bbq.ict  -> NativeLimbAttachModule.TryAddLimbAsNative(AbstractLimb)
bbo.icc  -> LimbDetachModule.TryDetach(AbstractLimb, AbstractCreature)
```
`bbn`'s three public methods are the sequential run `ibx`/`iby`/`ibz`, and their
signatures are distinct enough to place each one without relying on order —
which matters here, because **v0.1 declares them in a shuffled order** while
v0.14 does not. Cross-check: `bbn`'s two fields are `snw : bbq` and `snx : bbo`,
matching v0.14's `m_nativeLimbAttachModule` and `m_detachModule`.

`TryAddAsNative` is the exact inverse of the detach the context menu runs, and
**it will not put a severed limb back on.** Tested: arm cut off at the shoulder,
offered straight back to the body it came from, aiming at the torso and at the
arm — refused both times, 0 of 1. Severing evidently costs a limb its native
standing with the creature (`LimbNode.Native`, `vd.rva`, is a public setter, so
that is the first thing to try if this is ever revisited). It is for limbs that
were never removed, not for putting one back on. Use `vd.ham` and own the physics
— see `Items/SutureTool.cs`.

#### `vd` — `LimbNode`
```
rva -> Native (get/set)     rvb -> Parent      rvc -> HasParent
xfs -> Children             xft -> LimbHierarchyCallbacksReceivers
xfu -> CreatureHierarchyCallbacksReceivers     xfv -> CreatureHierarchyCallbacksSender
hal -> AddSystem(INodeSystem)                  ham -> AttachParent(LimbNode)
han -> DetachParent()       hao/hap -> AttachChildInternal / DetachChildInternal
```
Real run `hab`…`hap`; v0.14 declares Native, Parent, HasParent, Children in that
order, which is how the three getters were told apart.

`HasParent` is false for a severed limb *and* for a creature's root limb, so it
means "unheld", not "came off". A severed limb also gets a fresh creature of its
own (LimbDetachModule holds an `IAbstractCreatureFactory`), so its joint and its
transform parent both look perfectly reasonable — the node is the honest answer.

`ham` is the raw node link, below the protocols: it joins two nodes and tells
nobody, so the creature never learns it has a new limb. Use `ug.gwi` instead.

#### `ug` — `CreatureNodesHierarchy`
Reached as `bam.smd.sod` (`AbstractCreature.LimbsHierarchyHandler.Hierarchy`).
Real run `gwd`…`gwk`, in v0.14's declaration order, every signature matching its
position:
```
gwd -> ExternalEvents (uk = IExternalHierarchyEvents)
gwe -> AttachChildAsNative(LimbNode)        gwf -> CanChildBeAttachedAsNative(LimbNode)
gwg -> AttachParentAsNative(LimbNode)       gwh -> CanParentBeAttachedAsNative(LimbNode)
gwi -> AttachNodeAsThirdparty(attachedChildRoot, parent)
gwj -> DetachNodeFromParent(LimbNode, out IReadOnlyList<LimbNode>)
gwk -> SetNewRoot(LimbNode, out IReadOnlyList<LimbNode>, out LimbNode)
```
`gwi`, `gwj`, `gwk` are unambiguous on signature alone. `gwe`/`gwg` and
`gwf`/`gwh` are **not**: v0.1 strips the parameter names, and the xref
fingerprints do not carry across for this type — v0.14's `DetachNodeFromParent`
has 25 xrefs where v0.1's counterpart has 2, so the scan results are not
comparable here and the usual tell (see the decoy-tells notes) is unavailable.
Only the CallerCount *shape* survives: both `Can…` predicates are the two with a
high count in either version (3 in v0.14, 6 in v0.1), which confirms `gwf`/`gwh`
are the predicates without saying which is which.

Do not resolve the rest by guessing. **Ask the predicate and call its neighbour:**
`gwe`/`gwf` are adjacent in the sequence and so are `gwg`/`gwh`, so each attach
sits next to its own precondition, and the pairing holds even if "child" and
"parent" are the wrong way round. Both predicates are read-only, so asking both
costs nothing — which is what `Limbs.GraftNative` does.

**Confirmed in-game since:** a severed left arm offered to its own body reported
`as child True, as parent False`, and calling `gwe` parented it under
`Spine_1Prefab` with `Native` still true — which is `AttachChildAsNative`
behaving as itself and not `AttachParentAsNative`. So `gwe`/`gwf` are the child
pair and `gwg`/`gwh` the parent pair, by observation rather than by position.

**`gwi` is the one that makes a foreign limb part of a body.** Grafting a limb
that is not a creature's own is a first-class feature here, not a hole to climb
through: `ThirdpartyNodeAttachProtocol` sits beside `NativeChildNodeAttachProtocol`
and `NativeParentAttachProtocol`, and `AsChildLimbAttachData` carries a
`ThirdpartyRadiusIndexesDescription` next to the native one. Unlike
`TryAddAsNative` it asks nothing about whose limb it was and takes an explicit
parent; unlike `ham` it runs the attach protocol, which notifies
`NativeLimbListenersHandler` — the chain the puppeteer's own listener hangs off,
and therefore the only route by which a grafted limb gets animated instead of
hanging limp. Argument order is (child, parent), read off v0.14.

Related: `qb` = `AbstractPuppeteer`, whose `ggt` is `GetLimb(AbstractNodeTag)` —
the puppeteer addresses limbs **by node tag**, so a slot the body already has
filled has no second opening.

### `LimbPhysics` (real name in v0.1) — and the joint
The obfuscator left this type **entirely alone**: `hjr`…`hks` is an unbroken run
with no decoys, in v0.14's declaration order, and `m_joint` and `m_rb` kept their
real names. The joint holding a limb on is therefore a plain Unity
`ConfigurableJoint`, reachable and configurable without touching anything
obfuscated — which is the whole reason the Suture Tool can wire one by hand.
```
sam -> RbProvider (xf = ILimbRigidbodyProvider)   san -> JointProvider (xh)
sao -> Colliders (xe = ILimbCollider)             xhl -> Pivot (Transform)
xhm -> PivotPosition   xhn -> PivotRotation
xho -> RbPosition      xhp -> RbRotation
m_joint : ConfigurableJoint      m_rb : Rigidbody
```

#### `xh` — `LimbJointProvider`
Real run `hjg`…`hjq`.
```
hjg/hjh/hji -> MaximumForce / PositionDamper / PositionSpring  (get, order unverified)
hjj -> SetConnectedBody(ILimbRigidbodyProvider, Vector3)
hjk -> SetTargetRotation(Quaternion)      hjl -> SetSlerpDrive(JointDrive)
hjm/hjn -> SetLinearMotionType / SetAngularMotionType  (order unverified)
hjo/hjp -> SetBreakForce / SetBreakTorque              (order unverified)
hjq -> ProcessBreakEvent()
```
`hjj` is unambiguous on signature. Unused so far: what its `Vector3` means — world
anchor or connected anchor — is a guess, and configuring `m_joint` directly is
fully determined. Reach for it if the muscle drives turn out to ignore a joint
that was rewired behind the provider's back. Note v0.1 has no `get_ConnectedBody`;
read `m_joint.connectedBody`.


**Decoy note:** the obfuscator copies real *parameter* names into its decoys.
`vd.maj` and `vd.bee` both read `void (vd attachedParent)`, identical to the real
`ham`. Parameter names confirm what a method *is* once you have it; they do not
pick it out of a lineup. The sequential-name run still does. `xb` (`LimbUtils`)
is the extreme case — five `Public_Static_Boolean_Collider_byref_AbstractLimb`
overloads, all randomly named; going through `LimbEffectorReceiver` avoids
needing to choose.

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
