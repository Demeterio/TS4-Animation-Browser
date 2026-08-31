# Modern authored VFX / SWARM families

Fixture: see `../GAME_VERSION.md`.

This reference lists the authored family types present in the validated modern `VisualEffects` corpus and explains what is mechanically known about their role.

Build-specific authored-reference counts belong in `../GAME_VERSION.md` and are intentionally not duplicated here.

For named effects used as representative controls or authored-composition examples, see `TESTED_EFFECT_FIXTURES.md`.

## Validated family inventory

Active authored family types in the documented modern fixture:

```text
0x00 VisualEffect
0x01 ParticleEffect
0x02 MetaparticleEffect
0x03 DecalEffect
0x04 SequenceEffect
0x05 SoundEffect
0x06 ShakeEffect
0x0B GameEffect
0x0D DistributeEffect
0x0E RibbonEffect
0x0F SpriteEffect
```

Families present in the maintained type vocabulary but not populated in the documented modern fixture:

```text
0x07 CameraEffect
0x08 ModelEffect
0x09 ScreenEffect
0x0C FastParticleEffect
```

No `0x0A` family row occurs in the validated modern inventory, so no name is assigned to that numeric value.

Absence from this fixture is not a universal historical claim.

## Why family type is not enough to predict rendering

Do not use a rule such as:

```text
ParticleEffect -> one particle renderer
RibbonEffect   -> one ribbon renderer
SpriteEffect   -> one sprite shader
DecalEffect    -> one universal vertex layout
```

The real relationship is more flexible:

```text
authored family
  -> runtime effect/orchestration
  -> data-dependent simulation/resource resolution
  -> renderer route when graphics are needed
  -> runtime PRECOMP selection when applicable
```

Different authored families can converge on the same renderer family. One family can reach more than one renderer route depending on data. Some families perform sound, sequencing or gameplay/context work and do not require their own geometry renderer.

A faithful tool should therefore keep authored family data separate from route-specific renderer/PRECOMP observations.

## `0x00` VisualEffect

Primary role: child-effect orchestration.

The validated child path creates/uses real `EA::Swarm::cVisualEffect` runtime objects. The final ownership boundary was mechanically closed after separating the runtime Effect object from the `cParticlesDescription` object used elsewhere in the pipeline.

For modders/tool authors, the important point is that a VisualEffect reference is an **effect-to-effect edge**, not a new GPU primitive by itself.

## `0x01` ParticleEffect

This is the largest authored family in the documented modern fixture; exact counts are in `../GAME_VERSION.md`.

Runtime identities established for the validated build include:

```text
EA::Swarm::cParticlesDescription
EA::Swarm::cParticlesEffect
```

The Description -> Effect creation/retention path is closed, including authored lifetime storage and alignment dataflow.

Renderer behavior is data-dependent. Validated Particle fixtures reach, among other bounded cases:

- Classic particle/sprite geometry;
- Model Particle rendering using MODL/MLOD geometry;
- Dynamic `013f` rendering for at least one investigated case.

Do not treat every ParticleEffect as one vertex format or one shader pair.

Current first-party command-class vocabulary recovered and correctly attached includes examples such as:

```text
cParticleEnvironmentHideCommand
cParticleSpeedCommand
cParticleStretchCommand
cParticlePhysicsCommand
cParticleDirectLightingScaleCommand
cParticleAlignmentCommand
```

Current command strings also include proven vocabulary such as:

```text
presetLineAttractor
presetAttractor
worldWind
worldGravity
tractorTimeScale
```

A real command name still does not automatically recover the private member name or symbolic enum name of every serialized value it touches. Unknown private meanings remain numeric/bounded until independently proven.

## `0x02` MetaparticleEffect

Primary role: child-effect orchestration/simulation.

Validated records and runtime child handling are mechanically closed. The runtime creates, retains, updates and replaces real `cVisualEffect` children.

Current named command classes include examples such as:

```text
cMetaParticleDirectedWalkCommand
cMetaParticleMapCollideCommand
cMetaParticleMapRepelCommand
cMetaParticleMapAdvectCommand
```

A MetaparticleEffect therefore should not be modeled as “one metaparticle draw call”. Its visible result can come from the child effects it controls.

## `0x03` DecalEffect

The investigated current Decal case has an end-to-end mechanically closed renderer route, including runtime PRECOMP selection.

Current first-party runtime identities include:

```text
EA::Swarm::cDecalEffect
EA::Swarm::cDecalDescription
```

For that exact investigated route:

```text
runtime Decal owner/input
-> PRECOMP record + selector selection
-> exact direct-callback pass in the selector-1 scope
-> FUN_140B11640
-> slot 0 stride 44
-> slot 1 stride 4
-> 16-bit index buffer
-> DrawIndexed
```

This is **route-level evidence**, not a serialized meaning of family type `0x03`.

Do not encode:

```text
DecalEffect = always 44/4
```

The investigated runtime scope resolves several PRECOMP producer/record/selector combinations; those records are not claimed as globally/exclusively owned by the named effect.

A historical fixture-window RenderDoc `16/16` event is also real, but its exact Decal ownership was not established. Targeted causality/parent-scope audits do not support a `44/4 -> 16/16` transformation. Do not model one.

Current first-party command classes include examples such as:

```text
cDecalColorCommand
cDecalAlphaCommand
cDecalSizeCommand
cDecalAspectCommand
cDecalRotateCommand
cDecalLifeCommand
cDecalTextureCommand
cDecalMapEmitColorCommand
```

Private renderer-descriptor names remain intentionally conservative where only exact dataflow is known.

See `TESTED_EFFECT_FIXTURES.md`, `PACKAGE_TO_RENDER.md` and `RENDERER_REFERENCE.md` for the scope-separated end-to-end route.

## `0x04` SequenceEffect

Validated first-party runtime identities include:

```text
EA::Swarm::cSequenceEffect
EA::Swarm::cSequenceDescription
cSequenceWaitCommand
cSequencePlayCommand
```

Primary role: timed child-effect orchestration.

Validated behavior includes creation/replacement of real `cVisualEffect` children and start/stop command flags. A trailing serialized field remains raw/`UNKNOWN_BOUNDED` where no current first-party semantic name is attached.

A SequenceEffect may produce visible rendering through its children even though it does not need a unique geometry renderer.

## `0x05` SoundEffect

Primary role: audio/control behavior, not a geometry renderer.

Current first-party runtime identities established for this family include:

```text
EA::Swarm::cSoundDescription
EA::Swarm::cSoundEffect
```

Command/runtime vocabulary established in the validated research includes names such as:

```text
surfaceType
locationUpdateRate
length
volume
playByObjectVolume
hideFoggy
hideClear
noStop
```

A complete VFX preview can therefore need to consider sound even when the family itself does not submit vertices. An authored effect can combine SoundEffect with visual children/records.

Raw fields and private service identities remain bounded where the current executable does not attach a stronger name.

## `0x06` ShakeEffect

Primary role: a non-geometry runtime side effect using computed strength and a retained private endpoint.

A current first-party runtime identity is:

```text
EA::Swarm::cShakeEffect
```

Current first-party command classes include:

```text
cShakeLengthCommand
cShakeAspectCommand
cShakeBaseTableCommand
cShakeAmplitudeCommand
cShakeFrequencyCommand
```

The value-generation and dispatch mechanics are bounded, but the exact private endpoint/interface name is not proven. Do not publish a guessed name such as `ICameraShake` as an EA identity.

Examples in the validated corpus combine visual behavior and shake behavior. The shake path should not be mistaken for a missing particle draw.

## `0x0B` GameEffect

Primary role: gameplay/context service dispatch.

Validated first-party runtime identities include:

```text
EA::Swarm::cGameEffect
EA::Swarm::cGameDescription
```

Known command vocabulary includes `message` and `messageId`.

Behavior that depends on live gameplay state is naturally `CONTEXT_ONLY` for a standalone viewer/tool. That is different from an unknown renderer.

## `0x0D` DistributeEffect

Validated first-party runtime identities include:

```text
EA::Swarm::cDistributeEffect
EA::Swarm::cDistributeDescription
EA::Swarm::cSubdivOwner
```

Primary role: spatial child-effect orchestration/distribution.

Current first-party command classes include examples such as:

```text
cDistributeSourceCommand
cDistributeSubdivideCommand
cDistributeSurfaceCommand
cDistributeMapPinCommand
cDistributeMessageCommand
cDistributeClustersCommand
```

Other known controls include density, fit-children, pin-to-surface, static/scaling/width and Source/Subdivide/Surface/MapEmit-related data.

The documented modern fixture does not provide a non-zero nested Surface body suitable for promoting a historical nested structure as current-byte proof. Keep that boundary explicit until a current fixture supplies it.

## `0x0E` RibbonEffect

Runtime identities established for the validated build include:

```text
EA::Swarm::cRibbonDescription
EA::Swarm::cRibbonEffect
```

The Description -> Effect factory/retention route is closed.

Current named command classes include, among others:

```text
cRibbonWidthCommand
cRibbonTaperCommand
cRibbonSlipCurveCommand
cRibbonAnimateUVCommand
cRibbonSegmentLengthCommand
cRibbonMapAdvectCommand
```

At least one authored Ribbon (`ability_transform_sparkle_trail`) reaches the observed Dynamic `013f` GPU family. That is a **fixture-level proven connection**, not a universal equation:

```text
RibbonEffect != always Dynamic 013f
```

Private formula/member names that are not independently established remain bounded.

## `0x0F` SpriteEffect

Type `0x0F` in the validated build resolves to a factory-created:

```text
EA::Swarm::cBeamEffect
```

The exact repeated runtime handoff reaches a stable renderer-side interface, which can manage/queue renderer records. The private name of that returned interface remains unknown.

The validated Sprite tail data remains raw/unknown where no current first-party semantic name is attached.

## Families absent from the documented modern fixture

### CameraEffect (`0x07`)

No authored references are present in the documented modern fixture. This is a fixture statement, not a historical universal claim.

### ModelEffect (`0x08`)

No authored references are present in the documented modern fixture.

This is especially important because **Model Particle rendering is still very real**. Model Particle is a renderer behavior reached from particle data using MODL/MLOD geometry; it is not evidence that authored `ModelEffect` is populated.

### ScreenEffect (`0x09`)

No authored references are present in the documented modern fixture.

### FastParticleEffect (`0x0C`)

No authored references are present in the documented modern fixture.

## Tool design consequence

A faithful tool should classify family records by their real role instead of forcing every family through a GPU draw path:

```text
renderer-facing data       -> follow an evidence-proven renderer route
child orchestration        -> resolve/create/update child effects
sound/control              -> optional audio behavior
shake                      -> optional view/camera-side behavior
live gameplay service      -> context-only or explicitly unsupported outside the game
unknown bounded field      -> preserve and report, do not invent semantics
```

Do not reduce this to:

```text
authored family -> one hard-coded renderer
```

This avoids the common mistake of interpreting “nothing was drawn” as “the family is invalid” and prevents fixture-level physical facts from becoming false family-wide rules.
