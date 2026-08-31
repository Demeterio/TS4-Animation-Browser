# From a DBPF package in The Sims 4 to a rendered VFX

Fixture: see `../GAME_VERSION.md`.

This guide follows the proven VFX path for the documented fixture from DBPF resources on disk to CPU/runtime behavior, resource resolution, PRECOMP selection and the final D3D11 draw.

It is intentionally written as a **pipeline**, because most VFX mistakes come from collapsing several different layers into one.

Named effects used as representative controls for these stages are listed in `TESTED_EFFECT_FIXTURES.md`.

Build-specific corpus populations, fingerprints and closure metrics are centralized in `../GAME_VERSION.md` and are not repeated here. Build-specific executable/PRECOMP statements below refer to that validated fixture, not automatically to the latest live game patch.

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
7. Renderer-facing runtime data / route
        |
        v
8. Geometry / model / material / texture resolution as applicable
        |
        v
9. Runtime PRECOMP context + technique-selector selection when applicable
        |
        v
10. PRECOMP Effect-like record -> Technique -> Pass
        |
        v
11. VS / PS / CS source record + render-state slice
        |
        v
12. Raw Snappy -> DXBC -> D3D11 shader
        |
        v
13. D3D11 input/buffer/resource/state binding
        |
        v
14. Draw / DrawIndexed / DrawIndexedInstanced
```

Not every effect executes every box. `SoundEffect`, for example, can terminate at an audio/control service. `SequenceEffect` can spawn child effects that later reach the renderer. A child can itself contain several authored families.

The PRECOMP selection layer is also not a universal authored-family property. It belongs to the runtime renderer route that selected it.

### Named effect / PRECOMP cardinality

Do not model this relationship as:

```text
1 named effect = 1 PRECOMP record
```

The supported generic model is:

```text
named VFX / authored graph
-> one or more runtime branches
-> renderer route(s) when graphics are required
-> PRECOMP context + selector selection when applicable
```

That permits all of these valid outcomes:

```text
one named VFX     -> zero PRECOMP selections
one named VFX     -> one bounded PRECOMP selection
one named VFX     -> several PRECOMP selections across branches/routes
several named VFX -> shared PRECOMP context/record
```

A single authored VFX can combine, for example, a visual child reaching one PRECOMP route, another visual branch reaching another route, and a `SoundEffect` branch that never reaches GPU shader selection.

Structural PRECOMP compatibility, a matching shader pair or event proximity is therefore not exclusive named-effect ownership. Exact attribution requires a runtime/dataflow edge from the relevant effect/block/renderer context to the selected context + selector.

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

Deleted/tombstoned effective resources are not live content. A lower-priority shadowed copy must not be resurrected merely because it contains bytes that appear usable.

---

## 2. Effect identity and the split library

The collection-streaming representation observed in the validated build uses `VisualEffectsInstanceMap` to resolve an individual effect identity to the split resource that owns it:

```text
Effect IID
  -> MasterIID
  -> 0x1B192049:*:MasterIID
```

`MasterIID` can differ from the Effect IID. Several effect identities can live inside one split `VisualEffects` resource.

Exact ID resolution is therefore preferable to deduplicating only by effect name.

The complete build-specific identity audit belongs in `../GAME_VERSION.md` and `RESOURCE_TOPOLOGY.md`.

---

## 3. `VisualEffects` resource and VisualEffect definitions

The selected `0x1B192049 VisualEffects` resource contains one or more VisualEffect definitions.

The requested Effect IID identifies the target definition inside its owning resource.

A VisualEffect definition then references typed authored records. These records are the SWARM authored-family layer.

Documented fixture populations are centralized in `../GAME_VERSION.md`.

---

## 4. Authored SWARM family records

The validated modern fixture contains these active authored family types:

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

See `AUTHORED_FAMILIES.md` for per-family behavior and first-party command/runtime vocabulary established for the validated executable.

A physical vertex stride, PRECOMP record or draw call must not be treated as a universal serialized property of an authored family unless the serialized/runtime data itself proves that relationship.

---

## 5. Runtime Description / Effect creation

For several families in the validated build, the EA runtime uses a registry/factory pattern that separates authored/runtime Description data from the active Effect object.

The mechanically proven generic contract includes:

```text
family registry row
  -> Description factory
  -> retained/resolved Description
  -> Effect factory
  -> runtime Effect object
```

This matters because the serialized family body is not necessarily identical to the runtime renderer object.

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
- Ribbon and Sprite runtime-specific state construction;
- Decal queue/manager ownership, draw-record construction and stream preparation.

A tool should not report “renderer missing” merely because a family has no dedicated GPU geometry endpoint.

---

## 7. Renderer-facing route selection

When graphics are required, authored data in the validated build can reach several renderer behaviors.

Representative proven/bounded renderer families include:

```text
Classic particle/sprite
Model Particle, non-instanced
Model Particle, instanced
Dynamic 013f
Decal indexed route for the investigated case
Sprite/beam renderer-interface handoff
```

These names are not all private EA class names. Some are maintained descriptive labels for exact physical/dataflow behavior.

Do not derive the renderer solely from the authored family type or effect name.

The investigated `ep12_pom_high_skill_fail01_left` raw `drawMode = 0x83` case is especially useful here: its captured geometry follows the ordinary Model Particle/MLOD route, so that fixture does not justify inventing a distinct `0x83` geometry renderer.

---

## 8. Geometry, model, material and texture resource resolution

### Particle model-reference route

The maintained parser identifies exact Particle records and the primary resource field used by each record. Records on the proven model route resolve that primary IID as a MODL reference.

The resource-resolution result is classified explicitly:

```text
KNOWN            -> exact live effective MODL identified
MISSING          -> no live effective MODL target exists
UNKNOWN_BOUNDED  -> multiple candidates in the documented fixture prevent exact resolution
```

Do not replace a missing or ambiguous MODL with a same-IID resource of another type or an arbitrary fallback model.

Documented model-route populations are in `../GAME_VERSION.md`.

### MODL -> MLOD -> mesh

Validated Model Particle rendering resolves model geometry through:

```text
particle/model reference
  -> MODL
  -> MLOD
  -> MLOD source/mesh record
```

For the proven MLOD `0x206` source records, the maintained 24-byte interpretation includes:

```text
+0x00  name
+0x04  materialReference
+0x08  vertexFormatReference / VRTF
+0x0C  vertexBufferReference / VBUF
+0x10  indexBufferReference  / IBUF
+0x14  primitiveAndFlags
```

The VRTF/VBUF/IBUF references are therefore part of the model-geometry path, not data invented by a third-party renderer.

A zero encoded VRTF is a proven native case: the validated path can continue through the VBUF/IBUF resolution/upload route instead of treating zero VRTF as automatic mesh failure.

### Mesh -> material root

A mesh material reference can resolve directly to MATD or indirectly through MTST:

```text
mesh materialReference
-> MATD
```

or:

```text
mesh materialReference
-> MTST
-> serialized MTST entries
-> MATD leaves
```

A faithful tool must retain all serialized MTST entries in the normalized graph. It must not silently choose a convenient `variant == 0` or another arbitrary entry just to obtain a renderable material.

Documented mesh/material populations, serialized variant counts and material-state populations belong in `../GAME_VERSION.md`.

### MATD / ShaderData / textures

After the final MATD leaf is known, the material graph exposes ShaderData fields and resource keys that can resolve to textures and other resources.

The safe rule is:

```text
preserve exact serialized key
-> resolve exact effective resource
-> classify KNOWN / MISSING / unsupported explicitly
-> do not substitute another type or guessed texture
```

The documented corpus includes real missing ShaderData resource references. They are source-data outcomes, not parser failures. Exact populations are in `../GAME_VERSION.md`.

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

See `RENDERER_REFERENCE.md` for the exact bounded destination map.

### Decal geometry

For the investigated Decal route, preparation is mechanically closed from draw records through the physical streams:

```text
0x30-byte draw records
-> FUN_140B10AD0 allocation
-> slot 0 storage at owner+0x448, stride 44
-> slot 1 storage at owner+0x488, stride 4
-> index descriptor at owner+0x4C8
-> FUN_140B0A250 physical vertex writers
-> FUN_140B09D60 index writer
```

The 44/4 layout belongs to this proven renderer route. It is not a universal serialized property of authored family `0x03`.

---

## 9. Runtime PRECOMP context / selector selection

This is a distinct layer between renderer-facing runtime data and the structural PRECOMP record/pass data.

### Common transient resolver

The validated executable provides a fixture-independent mechanical bridge for the common transient route within the documented build.

A resource-backed branch reads an exact 32-bit field from a runtime resource/material-side object at `+0x4C` and feeds it into the context-resolution chain:

```text
runtime object+0x4C key
-> resource/context lookup
-> variant/state discriminator
-> resolved context candidate
```

The transient `0xA0` record then carries:

```text
+0x18  context candidate A
+0x20  context candidate B
+0x28  selector
```

The downstream bridge chooses `+0x20` only when the exact validated-runtime condition permits it and that field is non-zero; otherwise it uses `+0x18`. The private business meaning of the condition is intentionally not guessed.

The pair is transported through per-thread storage and consumed exactly as:

```text
selected context
+
record +0x28 selector
-> FUN_1413689E0(context, selector, ...)
```

The no-resource/default branch can construct contexts from first-party hashed names observed in the validated build, including `particle` and `ParticleLight`. Therefore the common resolver is **not** equivalent to a universal direct MATD-key lookup.

Important boundary:

```text
runtime object+0x4C    -> PRECOMP context-resolution chain      PROVEN
serialized MATD Shader -> runtime object+0x4C                   UNKNOWN_BOUNDED / not established
runtime object+0x4C    == PRECOMP outer effect-like +0x00       UNKNOWN_BOUNDED / not established universally
```

Those two bounded relations are not blocking research tasks. Reopen them only if a future implementation requirement, contradictory fixture or changed game build needs the missing causal edge.

### SceneModel resolver

The validated SceneModel path independently proves:

```text
context = explicit record context
       OR table-selected owner context slot / fallback

selector = record selector when non-negative
       OR owner-side selector fallback

-> FUN_1413689E0(context, selector, ...)
```

Private names for those owner slots/table values are not required for correct selection mechanics and remain structural.

### Investigated Decal route

For the investigated Decal route, its own producer chain remains mechanically closed:

```text
FUN_140B0C220
-> FUN_140A2A7C0
-> FUN_140A2A8F0
-> FUN_140A25C80 / FUN_140A2C5E0
-> PRECOMP context + selector
-> FUN_1413689E0
```

The exact runtime probes establish producer -> selected context/selector -> shared submission identity for the accepted investigated scope. Build-specific observation counts are internal/regression details rather than generic pipeline constants.

A narrower selector-1 scope resolves the direct callback to:

```text
FUN_140B11640
```

and exact PRECOMP pass/shader/state-slice rows are retained in the normalized runtime evidence.

The maintained `male_child_blue` fixture also has exact fixture-scoped PRECOMP attribution. That result is useful regression evidence, but it is not a family-wide Model Particle default.

The common resolver mechanics do **not** manufacture exact record ownership where no effect/block -> record/selector observation exists. For example, a set of shader-compatible candidates must remain non-unique if the runtime ownership edge has not been observed.

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

A Pass contains dataflow-proven stage/state references:

```text
VSRef
PSRef
CSRef
StateStart
StateCount
optional relative field
```

Reference rule:

```text
0     = no shader for that stage
N > 0 = 1-based index into that stage's source table
```

No PRECOMP binary-format change is implied when a runtime route is newly joined to an existing record/pass. Runtime evidence can improve attribution without changing the serialized ABI.

---

## 11. Stage source -> Raw Snappy -> DXBC

For every recovered stage source record in the documented fixture, source record `+0x00` resolves to the exact physical compact stream used by that entry.

The compact stream is:

```text
Raw Snappy
  -> decompression
  -> DXBC
```

The reconstructed DXBC was validated through Direct3D reflection/disassembly and the matching D3D11 shader-creation path.

Shader populations for the documented fixture are centralized in `../GAME_VERSION.md`.

---

## 12. PRECOMP render-state consumer

A Pass also selects a serialized state slice:

```text
StateStart / StateCount
-> {stateId, rawValue}
-> common cached setter
-> common EA state dispatcher
-> D3D11 depth/stencil, rasterizer and blend state/bind parameters
```

This is a **generic consumer relationship**, not a fixture heuristic.

The validated dispatcher mechanically handles IDs `0x00..0x1E`. Exact mappings and value translators are documented in:

```text
../precomp/guides/FORMAT_REFERENCE.md
```

The important semantic boundary is state `0x1B`: its descriptor transformation is known exactly, while its original private conceptual EA name remains `UNKNOWN_BOUNDED`.

Fixture RenderDoc snapshots are kept separately as regression evidence. They answer “what was bound for this captured draw?” but cannot define the generic meaning of a state ID.

---

## 13. Material and renderer-state layer

A model source record contains a `materialReference`, and the renderer must also resolve/bind the textures, samplers, constants and state needed by the selected technique/pass.

Do **not** assume that every material relation is already a recovered private EA name.

One important PRECOMP example is the validated DX11 outer effect-like record `+0x00`: it is strongly correlated with known MATD Shader hashes and has an exact equality on a maintained fixture, but the maintained templates deliberately keep the field structurally named/raw because a universal direct `MATD.shader_key -> PRECOMP record` selector relation has not been proven.

The common runtime resolver narrows the unknown without erasing it: runtime object `+0x4C` is a proven causal key in PRECOMP context resolution, while assignment from the serialized MATD Shader field remains `UNKNOWN_BOUNDED` for the documented fixture. Likewise, no universal equality/index relation with PRECOMP outer `+0x00` is claimed.

Historical material-field candidates are also not promoted merely because they resemble blend settings. For the documented fixture, renderer state comes from exact serialized PRECOMP state plus validated consumer mechanics, unless a separate material-to-state relation is independently proven.

Safe rule:

```text
resolve and preserve exact resource IDs / state records
-> use proven mappings where available
-> keep unknown material/state semantics explicit
```

---

## 14. D3D11 resource binding

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

For the exact investigated Decal callback, `FUN_140B11640` binds:

```text
state/binding object -> owner+0x440
slot 0               -> owner+0x448, stride 44
slot 1               -> owner+0x488, stride 4
index                -> owner+0x4C8, 16-bit
```

---

## 15. Draw submission

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
- the exact investigated Decal route uses `DrawIndexed` after the 44/4 + 16-bit index-buffer binding;
- compute shaders exist in the PRECOMP data, but a CS reference is not automatically part of every visual draw.

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
-> renderer/resource context construction
-> context + selector
-> PRECOMP Technique/Pass when exact selection is available
-> VS + PS references
-> PRECOMP state slice
-> Raw Snappy -> DXBC
-> D3D11 bindings/state
-> draw
```

Representative named controls include `clue_sparkle` and `make_it_rain_c`.

An exact common context/selector mechanism does not by itself turn `clue_sparkle`'s multiple compatible PRECOMP candidates into unique ownership.

## Example B — instanced Model Particle

```text
DBPF
-> VisualEffects
-> ParticleEffect with model behavior
-> model reference
-> MODL -> MLOD
-> mesh VRTF/VBUF/IBUF
-> mesh materialReference -> MATD/MTST -> MATD leaf
-> mesh vertex/index streams
-> per-instance slot 1 stream, stride 124
-> exact fixture PRECOMP attribution where available
-> D3D11 vertex/index/resource/state bindings
-> DrawIndexedInstanced
```

Representative control: `male_child_blue`.

## Example C — Dynamic `013f`

```text
DBPF
-> VisualEffects
-> authored ParticleEffect or RibbonEffect in a proven fixture
-> runtime family-specific preparation
-> Dynamic 013f renderer route
-> renderer/PRECOMP selection where proven
-> D3D11 submission
```

`ability_transform_sparkle_trail` and `gp07_mother_plant_spray` are useful because they prove convergence from different authored-family contexts without establishing a family-wide renderer equation.

## Example D — investigated Decal

```text
DBPF
-> VisualEffects
-> DecalEffect
-> runtime Decal owner/input
-> PRECOMP producer/context + selector
-> selected Pass/shader refs/state slice
-> FUN_140B11640
-> 44-byte slot 0 + 4-byte slot 1 + UINT16 index buffer
-> DrawIndexed
```

The historical 16/16 RenderDoc event remains a separate capture-window observation. No 44/4-to-16/16 bridge is modeled.

## Example E — orchestration/audio/context

An authored VFX can combine child VisualEffects, SoundEffect, ShakeEffect or GameEffect with visible renderer-facing children.

A standalone tool should preserve those branches and report context-only behavior instead of declaring them invalid because they do not each produce geometry.

---

# Implementation rules

A faithful consumer should prefer:

```text
exact serialized bytes
-> exact effective resource graph
-> proven runtime/renderer route
-> proven context/selector mechanics
-> proven PRECOMP/pass/state mechanics
```

over:

```text
effect-name heuristic
historical material field assumption
shader-hash candidate promoted to unique ownership
fixture-wide extrapolation
candidate ranking promoted to fact
```

Distinguish:

```text
invalid resource
missing referenced resource
unsupported structure
unknown bounded semantic/provenance
```

Do not silently coerce one category into another.

When a private name is unavailable but the mechanics are sufficient, keep the raw value/structural label and document the evidence boundary rather than inventing a name.
