# Modern authored VFX / SWARM families

Fixture: see `../GAME_VERSION.md`.

This reference lists the authored family types actually present in the validated modern `VisualEffects` corpus and explains what is mechanically known about their role.

The counts are **authored-family references**, not renderer draw counts and not counts of unique effect names.

For named effects used as representative controls or authored-composition examples, see `TESTED_EFFECT_FIXTURES.md`.

## Validated inventory

```text
0x00 VisualEffect           9,802
0x01 ParticleEffect        80,621
0x02 MetaparticleEffect    17,684
0x03 DecalEffect               23
0x04 SequenceEffect           497
0x05 SoundEffect            7,274
0x06 ShakeEffect                9
0x0B GameEffect                 6
0x0D DistributeEffect           2
0x0E RibbonEffect           1,027
0x0F SpriteEffect              22
```

Zero population in the validated modern corpus:

```text
0x07 CameraEffect        0
0x08 ModelEffect         0
0x09 ScreenEffect        0
0x0C FastParticleEffect  0
```

No `0x0A` family row occurs in the validated modern inventory, so no name is assigned to that numeric value.

A zero count means “not observed in these 12,430 modern `VisualEffects` resources”. It is not a universal historical claim.

## Why family type is not enough to predict rendering

Do not use a rule such as:

```text
ParticleEffect -> one particle renderer
RibbonEffect   -> one ribbon renderer
SpriteEffect   -> one sprite shader
```

The real relationship is more flexible:

```text
authored family
  -> runtime effect/orchestration
  -> data-dependent simulation/resource resolution
  -> renderer family when graphics are needed
```

Different authored families can converge on the same renderer family. Some families perform sound, sequencing or gameplay/context work and do not require their own geometry renderer.

## `0x00` VisualEffect

**Validated refs:** 9,802

Primary role: child-effect orchestration.

The validated child path creates/uses real `EA::Swarm::cVisualEffect` runtime objects. The final ownership boundary was mechanically closed after separating the runtime Effect object from the `cParticlesDescription` object used elsewhere in the pipeline.

For modders/tool authors, the important point is that a VisualEffect reference is an **effect-to-effect edge**, not a new GPU primitive by itself.

## `0x01` ParticleEffect

**Validated refs:** 80,621

This is by far the largest authored family in the validated corpus.

Runtime identities established for the validated build include:

```text
EA::Swarm::cParticlesDescription
EA::Swarm::cParticlesEffect
```

The Description -> Effect creation/retention path is closed, including authored lifetime storage and `alignMode` dataflow.

Renderer behavior is data-dependent. Validated Particle fixtures reach, among other bounded cases:

- Classic particle/sprite geometry;
- Model Particle rendering using MODL/MLOD geometry;
- Dynamic `013f` rendering for at least one investigated case.

Do not treat every ParticleEffect as one vertex format or one shader pair.

Several private flag meanings and the original names for alignment values `9..12` remain bounded/unknown and should be kept numeric unless independently proven.

## `0x02` MetaparticleEffect

**Validated refs:** 17,684

Primary role: child-effect orchestration/simulation.

Validated v4 records and runtime child handling are mechanically closed. The runtime creates, retains, updates and replaces real `cVisualEffect` children.

A MetaparticleEffect therefore should not be modeled as “one metaparticle draw call”. Its visible result can come from the child effects it controls.

## `0x03` DecalEffect

**Validated refs:** 23

The investigated Decal family has a dedicated renderer route with packed geometry, explicit renderer-state ownership and indexed drawing.

Known command vocabulary includes values such as:

```text
color / colour
alpha
size
aspect
rotate
life
texture
mapEmitColor / mapEmitColour
```

Private renderer-descriptor names remain intentionally conservative where only exact dataflow is known.

## `0x04` SequenceEffect

**Validated refs:** 497

Validated runtime identities include:

```text
cSequenceEffect
cSequenceDescription
cSequenceWaitCommand
cSequencePlayCommand
```

Primary role: timed child-effect orchestration.

Validated behavior includes creation/replacement of real `cVisualEffect` children and start/stop command flags. A trailing serialized 4-byte field remains raw/`UNKNOWN_BOUNDED`.

A SequenceEffect may produce visible rendering through its children even though it does not need a unique geometry renderer.

## `0x05` SoundEffect

**Validated refs:** 7,274

Primary role: audio/control behavior, not a geometry renderer.

Command/runtime names established in the validated research include:

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

Some raw fields and private service identities remain `UNKNOWN_BOUNDED`.

## `0x06` ShakeEffect

**Validated refs:** 9

Primary role: a non-geometry runtime side effect using computed strength and a retained private endpoint.

The value-generation and dispatch mechanics are bounded, but the exact private endpoint/interface name is not proven. Do not publish a guessed name such as `ICameraShake` as an EA identity.

Examples in the validated corpus include effects where a visual event and a shake can be authored together. The shake behavior should not be mistaken for a missing particle draw.

## `0x0B` GameEffect

**Validated refs:** 6

Primary role: gameplay/context service dispatch.

Validated runtime identities include `cGameEffect` / `cGameDescription`; known command names include `message` and `messageId`.

Behavior that depends on live gameplay state is naturally `CONTEXT_ONLY` for a standalone viewer/tool. That is different from an unknown renderer.

## `0x0D` DistributeEffect

**Validated refs:** 2

Validated runtime identities include:

```text
cDistributeEffect
cDistributeDescription
cSubdivOwner
```

Primary role: spatial child-effect orchestration/distribution.

Known controls include `density`, `fitChildren`, `pinToSurface`, `forceStatic`, `scale`, `width` and Source/Subdivide/Surface/MapEmit-related data.

The only serialized Distribute record observed in the validated corpus has `surfaceCount=0`. A historical non-zero nested `Surface` / `SurfacePoint` body therefore cannot be claimed as current-byte proven without a validated non-zero fixture.

## `0x0E` RibbonEffect

**Validated refs:** 1,027

Runtime identities established for the validated build include:

```text
EA::Swarm::cRibbonDescription
EA::Swarm::cRibbonEffect
```

The Description -> Effect factory/retention route is closed.

At least one authored Ribbon (`ability_transform_sparkle_trail`) reaches the observed Dynamic `013f` GPU family. That is a **fixture-level proven connection**, not a universal equation:

```text
RibbonEffect != always Dynamic 013f
```

Private formula/member names that are not independently established remain bounded.

## `0x0F` SpriteEffect

**Validated refs:** 22

Type `0x0F` in the validated build resolves to a factory-created:

```text
EA::Swarm::cBeamEffect
```

The exact repeated runtime handoff reaches a stable renderer-side interface, which can manage/queue renderer records. The private name of that returned interface remains unknown.

The validated Sprite v5 constant 20-byte tail remains raw/unknown and must not receive an invented semantic name.

## Zero-population families in the validated modern corpus

### CameraEffect (`0x07`)

No authored references in the validated modern corpus.

### ModelEffect (`0x08`)

No authored references in the validated modern corpus.

This is especially important because **Model Particle rendering is still very real**. Model Particle is a renderer behavior reached from particle data using MODL/MLOD geometry; it is not evidence that authored `ModelEffect` is populated.

### ScreenEffect (`0x09`)

No authored references in the validated modern corpus.

### FastParticleEffect (`0x0C`)

No authored references in the validated modern corpus.

## Viewer/tool design consequence

A faithful tool should classify family records by their real role instead of forcing every family through a GPU draw path:

```text
renderer-facing data       -> render with the appropriate proven route
child orchestration        -> resolve/create/update child effects
sound/control              -> optional audio behavior
shake                      -> optional camera/view behavior
live gameplay service      -> context-only or explicitly unsupported outside the game
unknown bounded field      -> preserve and report, do not invent semantics
```

This avoids the common mistake of interpreting “nothing was drawn” as “the family is invalid”.
