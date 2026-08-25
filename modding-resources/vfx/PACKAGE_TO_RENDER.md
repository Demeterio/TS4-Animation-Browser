# From a DBPF package in The Sims 4 to a rendered VFX

Fixture: see `../GAME_VERSION.md`.

This guide follows the proven VFX path for the documented fixture from DBPF resources on disk to CPU/runtime behavior, resource resolution, PRECOMP shader selection and the final D3D11 draw.

It is intentionally written as a **pipeline**, because most VFX mistakes come from collapsing several different layers into one.

Named effects used as representative controls for these stages are listed in `TESTED_EFFECT_FIXTURES.md`.

## Pipeline overview

```text
1. DBPF package/index
        |
        v
2. Effective VFX resource + effect identity
        |
        v
3. VisualEffect definition
        |
        v
4. Authored SWARM family records
        |
        v
5. Runtime Description / Effect / child orchestration
        |
        v
6. CPU simulation or non-GPU endpoint
        |
        v
7. Renderer family when graphics are required
        |
        v
8. Model / geometry / material / texture resource resolution
        |
        v
9. PRECOMP Effect-like record -> Technique -> Pass
        |
        v
10. VS / PS / CS source record
        |
        v
11. Raw Snappy -> DXBC -> D3D11 shader
        |
        v
12. D3D11 input/buffer/resource/state binding
        |
        v
13. Draw / DrawIndexed / DrawIndexedInstanced
```

Not every effect executes every box. `SoundEffect`, for example, can terminate at an audio/control service. `SequenceEffect` can spawn child effects that later reach the renderer. A child can itself contain several authored families.

---

## 1. DBPF package and effective resource selection

The Sims 4 stores VFX resources inside DBPF packages. A resource is identified by its TGI values:

```text
Type : Group : Instance
```

A tool must first identify the **effective** resource after normal package/override resolution. Do not assume that finding a matching resource in one package means that copy is the one the game will use.

The validated VFX library uses three relevant resource types:

```text
0x1B192049  VisualEffects
0x1B19204A  VisualEffectsInstanceMap
0xEA5118B0  VisualEffectsMerged
```

See `RESOURCE_TOPOLOGY.md` for the exact split/merged relationship.

---

## 2. Effect identity and the split library

The collection-streaming representation observed in the validated build uses `VisualEffectsInstanceMap` to resolve an individual effect identity to the split resource that owns it:

```text
Effect IID
  -> MasterIID
  -> 0x1B192049:*:MasterIID
```

`MasterIID` can differ from the Effect IID. Several effect identities can live inside one split `VisualEffects` resource.

The documented merged `.swb` contains 33,852 effect identities and the documented `.swh2` contains 33,852 matching `Effect IID -> MasterIID` links. All 33,852 resolve to existing split resources in the validated corpus.

For a modern tool, exact ID resolution is therefore preferable to deduplicating only by effect name.

---

## 3. `VisualEffects` resource and VisualEffect definitions

The selected `0x1B192049 VisualEffects` resource contains one or more VisualEffect definitions. Across the validated 12,430 decoded resources the corpus contains:

```text
33,852 VisualEffect definitions
```

The requested Effect IID identifies the target definition inside its owning resource.

A VisualEffect definition then references typed authored records. These records are the SWARM authored-family layer.

---

## 4. Authored SWARM family records

The validated modern corpus contains 11 active authored families:

```text
VisualEffect
ParticleEffect
MetaparticleEffect
DecalEffect
SequenceEffect
SoundEffect
ShakeEffect
GameEffect
DistributeEffect
RibbonEffect
SpriteEffect
```

The most important rule is:

```text
authored family != renderer family
```

A family record describes authored behavior/data. It may create a runtime effect, spawn child effects, perform audio/control work, depend on gameplay context or feed one of several GPU renderers.

See `AUTHORED_FAMILIES.md` for the exact validated counts and per-family behavior.

---

## 5. Runtime Description / Effect creation

For several families in the validated build, the EA runtime uses a generic family registry/factory pattern that separates authored/runtime Description data from the active Effect object.

The mechanically proven generic contract includes:

```text
family registry row
  -> Description factory
  -> retained/resolved Description
  -> Effect factory
  -> runtime Effect object
```

This is important for tools because the serialized family body is not necessarily identical to the runtime renderer object.

Examples include validated Particle, Ribbon and Sprite routes.

For Particle specifically, the validated runtime distinguishes:

```text
EA::Swarm::cParticlesDescription
EA::Swarm::cParticlesEffect
```

The Effect retains/resolves Description data and later runtime paths consume that data during simulation/render preparation.

---

## 6. CPU simulation, orchestration and non-GPU endpoints

Before D3D11 becomes relevant, the VFX can perform substantial CPU-side work.

Examples:

- Particle lifetime, curves, alignment and emitted-particle state;
- child-effect creation/replacement for VisualEffect and MetaparticleEffect;
- timed Play/Wait behavior for SequenceEffect;
- audio/control behavior for SoundEffect;
- strength generation/dispatch for ShakeEffect;
- live gameplay/context dispatch for GameEffect;
- spatial child distribution for DistributeEffect;
- Ribbon and Sprite runtime-specific state construction.

A tool should not report “renderer missing” merely because a family has no dedicated GPU geometry endpoint.

---

## 7. Renderer-family selection

When graphics are required, authored data in the validated build can reach one of several renderer behaviors.

Representative proven/bounded renderer families include:

```text
Classic particle/sprite
Model Particle, non-instanced
Model Particle, instanced
Dynamic 013f
Decal
ep12_pom_high_skill_fail01_left raw drawMode 0x83 case -> ordinary Model Particle/MLOD geometry route
```

These names are not all private EA class names. Some are maintained descriptive labels for exact physical/dataflow behavior.

Do not derive the renderer solely from the authored family type or effect name.

---

## 8. Geometry and model resource resolution

### Model Particle route

Validated Model Particle rendering can resolve model geometry through:

```text
particle/model reference
  -> MODL
  -> MLOD
  -> MLOD source record
```

For the proven MLOD `0x206` source records, the record is 24 bytes:

```text
+0x00  name
+0x04  materialReference
+0x08  vertexFormatReference / VRTF
+0x0C  vertexBufferReference / VBUF
+0x10  indexBufferReference  / IBUF
+0x14  primitiveAndFlags
```

The VRTF/VBUF/IBUF references are therefore part of the model-geometry path, not data invented by the viewer.

A zero encoded VRTF is a proven native case: the validated path can continue through the VBUF/IBUF resolution/upload route instead of treating zero VRTF as automatic mesh failure.

Validated-corpus diagnostics intentionally preserve unresolved outcomes:

```text
17,578 model-bit ParticleEffect references
17,478 MODL -> MLOD resolved
72     unresolved IID
20     no model candidate
8      model decode failures
```

Those classes should remain explicit. Do not silently replace an unresolved model with arbitrary fallback geometry.

### Classic particle geometry

The bounded Classic vertex stream is 24 bytes per vertex:

```text
+0x00  POSITION0  R32G32B32_FLOAT
+0x0C  COLOR0     R8G8B8A8_UNORM
+0x10  TEXCOORD0  R32G32_FLOAT
```

The CPU/runtime route builds dynamic geometry that is later bound as a D3D11 vertex stream.

### Instanced Model Particle

The proven instanced path adds a second per-instance stream:

```text
slot      = 1
stride    = 0x7C = 124 bytes
step rate = 1
```

The record carries a position/scale term, local basis vectors and packed normal-derived terms used by the captured shader. See `RENDERER_REFERENCE.md` for the exact bounded layout and the meaning of the captured IA semantic column.

---

## 9. Material, texture and renderer-state layer

A model source record contains a `materialReference`, and the renderer must also resolve/bind the textures, samplers, constants and state needed by the selected technique/pass.

Do **not** assume that every material relation is already a recovered private EA name.

One important PRECOMP example is the validated DX11 outer effect-like record `+0x00`: it is strongly correlated with known MATD Shader hashes, but the maintained templates deliberately keep the field structurally named/raw because the exact semantic attachment has not been promoted to a proven private field identity.

Likewise, PRECOMP pass state slices have proven storage/dataflow bounds without every state-pair word being renamed into a guessed D3D11 business meaning.

For tool authors, the safe rule is:

```text
resolve and preserve exact resource IDs / state records
-> use proven mappings where available
-> keep unknown material/state semantics explicit
```

---

## 10. PRECOMP Effect-like record -> Technique -> Pass

The Direct3D 11 shader package used for the validated fixture is:

```text
Game\Bin\res\Shaders_DX11.precomp
```

The validated hierarchy is:

```text
DATA
  -> root entry "shaders"
    -> payload Version 6
      -> outer effect-like record
        -> Technique
          -> Pass
```

The outer `Effect` / `Dx11EffectRecord` wording is a maintained structural label. It is not a claim that the stripped EA private type has exactly that name.

`Technique` and `Pass` are both structurally proven in the validated representation and additionally have first-party historical PRECOMP vocabulary behind them.

A Pass contains dataflow-proven stage references:

```text
VSRef
PSRef
CSRef
```

Reference rule:

```text
0     = no shader for that stage
N > 0 = 1-based index into that stage's source table
```

---

## 11. Stage source -> Raw Snappy -> DXBC

Validated DX11 shader corpus:

```text
37,960 total
 7,947 VS
30,009 PS
     4 CS
```

For every recovered stage source record, the source record's first 32-bit value resolves to the exact physical compact stream used by that entry.

The compact stream is:

```text
Raw Snappy
  -> decompression
  -> DXBC
```

The reconstructed DXBC was validated through Direct3D reflection/disassembly and the matching D3D11 shader-creation path.

This closes the distinction between:

```text
PRECOMP metadata/reference
physical compressed shader bytes
compiled DXBC shader
```

---

## 12. D3D11 resource binding

A shader alone does not make a draw.

The renderer also binds, as applicable:

```text
input layout
vertex buffer(s)
index buffer
constant buffer(s)
textures / shader resources
samplers
blend state
depth/stencil state
rasterizer/culling state
VS / PS / CS
```

The exact set depends on the renderer family/technique.

The shared renderer research for the validated build proves the normal D3D11 operations for vertex/index binding and the draw calls used by the bounded VFX routes.

---

## 13. Draw submission

Observed final submission families include:

```text
Draw
DrawIndexed
DrawIndexedInstanced
```

Examples:

- Classic dynamic geometry can use non-indexed or route-specific submission depending on the technique;
- Model Particle non-instanced uses model geometry through the shared indexed path;
- the proven instanced Model Particle route uses `DrawIndexedInstanced` with the extra 124-byte instance stream;
- Decal uses indexed geometry;
- compute shaders exist in the PRECOMP corpus, but a CS reference is not automatically part of every visual draw.

---

# End-to-end examples

## Example A — Classic Particle

```text
DBPF
-> VisualEffects resource
-> requested VisualEffect definition
-> ParticleEffect
-> cParticlesDescription / cParticlesEffect runtime data
-> CPU particle simulation/builder
-> dynamic 24-byte POSITION/COLOR/TEXCOORD vertices
-> material/renderer-state selection
-> PRECOMP Technique/Pass
-> VS + PS references
-> Raw Snappy -> DXBC
-> D3D11 bindings
-> draw
```

Representative named controls include `clue_sparkle` and `make_it_rain_c`; see `TESTED_EFFECT_FIXTURES.md` for the exact scope of each.

## Example B — instanced Model Particle

```text
DBPF
-> VisualEffects
-> ParticleEffect with model behavior
-> model reference
-> MODL -> MLOD
-> materialReference + VRTF/VBUF/IBUF
-> mesh vertex/index streams
-> per-instance slot 1 stream, stride 124
-> PRECOMP pass/shaders
-> D3D11 vertex/index/resource bindings
-> DrawIndexedInstanced
```

The authoritative named control for the bounded 124-byte instance map is `male_child_blue`.

## Example C — Ribbon

```text
DBPF
-> VisualEffects
-> RibbonEffect
-> cRibbonDescription
-> cRibbonEffect
-> runtime renderer-facing data
-> renderer family selected from actual runtime/data
-> PRECOMP pass/shaders
-> D3D11 draw
```

`ability_transform_sparkle_trail` reaches Dynamic `013f` in the validated fixture. Do not generalize that mapping to every Ribbon.

## Example D — Sound plus visual behavior

```text
VisualEffect definition
  -> SoundEffect -> audio/control endpoint
  -> ParticleEffect / child VisualEffect -> graphics path
```

The absence of a SoundEffect draw is correct. The visible portion may come from another family record in the same authored effect graph. `lightning_flash_full` and `ep18_world_4spout_fountain` are named composition/runtime examples documented in `TESTED_EFFECT_FIXTURES.md`.

---

## What a robust parser/viewer should report explicitly

Keep these outcomes distinct:

```text
invalid resource
missing resource
unsupported version/branch
resolved but non-renderer family
context-only behavior
unknown-bounded semantic
GPU renderer path available
shader/material/resource resolution failure
```

Silently converting all of them into “nothing to render” makes debugging and modding much harder.
