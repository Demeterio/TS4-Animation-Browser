# VFX renderer reference

Fixture: see `../GAME_VERSION.md`.

This document collects renderer-side facts that are useful to modders and tool authors after an authored VFX has reached a graphics path.

It does **not** define a one-to-one authored-family mapping. Renderer membership must come from actual data/runtime behavior.

For the named effects used as controls for these routes, see `TESTED_EFFECT_FIXTURES.md`.

Build-specific corpus populations and closure metrics are centralized in `../GAME_VERSION.md`. Native `FUN_...` labels, absolute addresses and RVAs on this page are **build-specific evidence anchors for the validated executable**, not stable APIs or cross-build identifiers. Re-establish them if the executable changes.

## Shared D3D11 endpoint

The bounded VFX routes converge on ordinary Direct3D 11 resource binding and draw submission.

Relevant final operations include:

```text
IA vertex-buffer binding
IA index-buffer binding
Draw
DrawIndexed
DrawIndexedInstanced
```

The exact input layout, buffers, shaders, resources and state vary by technique.

## Renderer classifications represented by validated evidence

The maintained renderer model distinguishes these reusable route categories:

```text
Classic particle/sprite
Model Particle non-instanced
Model Particle instanced
Dynamic 013f
Decal
Sprite / beam renderer-interface handoff
```

These are renderer classifications, not authored SWARM families and not all private EA class names.

## Classic particle / sprite geometry

Exact bounded vertex interface:

| Offset | Semantic | DXGI-style format | Size |
| ---: | --- | --- | ---: |
| `0x00` | `POSITION0` | `R32G32B32_FLOAT` | 12 |
| `0x0C` | `COLOR0` | `R8G8B8A8_UNORM` | 4 |
| `0x10` | `TEXCOORD0` | `R32G32_FLOAT` | 8 |

```text
stride = 24 bytes
```

`POSITION`, `COLOR` and `TEXCOORD` are shader/input semantics. They do not by themselves prove the original business meaning of every CPU source field.

Classic-looking VFX can still select different shader/state combinations; the vertex stride alone is not a complete material identity.

`clue_sparkle` and `make_it_rain_c` are useful regression controls precisely because they can share Classic-style geometry/shader characteristics while using different output-merger behavior.

## Model Particle resource graph

Validated Model Particle geometry resolves through real game resources:

```text
Particle model reference
-> MODL
-> MLOD
-> mesh
-> VRTF / VBUF / IBUF
-> materialReference
-> MATD directly
   or
-> MTST -> serialized MATD leaves
-> ShaderData/resource keys/textures as applicable
```

The normalized graph keeps resource-resolution outcomes explicit. A missing MODL, missing ShaderData target or unsupported structure is not evidence for another renderer family and must not silently fall back to arbitrary geometry/materials.

Documented model/material/texture populations belong in `../GAME_VERSION.md`.

## Model Particle — non-instanced

A proven MLOD `0x206` source record uses this 24-byte layout:

```text
+0x00  name
+0x04  materialReference
+0x08  vertexFormatReference / VRTF
+0x0C  vertexBufferReference / VBUF
+0x10  indexBufferReference  / IBUF
+0x14  primitiveAndFlags
```

The route then resolves/uploads the vertex/index data and uses the shared indexed model-renderer path.

### Zero VRTF

An encoded VRTF reference of zero is not automatically a fatal mesh error on the proven route. The native path can continue with VBUF/IBUF resolution/upload using its zero-VRTF handling.

Do not replace that behavior with “VRTF zero means invalid model” without checking the actual case.

### Layout variation is real

Different non-instanced Model Particle fixtures use different captured IA layouts/strides. This is why a renderer family cannot be reduced to one universal mesh vertex declaration.

The maintained controls include:

```text
accursed_hand_04
ep05_flowers_table_hand_chrysanthemum_white
ep12_pom_high_skill_fail01_left
```

Their exact fixture draw/stride details are kept in `TESTED_EFFECT_FIXTURES.md`.

For EP12, installed MLOD geometry matches the captured IA input exactly for the compared candidates, while unique authored draw ownership remains bounded. Geometry identity and ownership are separate claims.

## Model Particle — instanced

The proven instanced route uses model geometry plus a per-instance vertex stream:

```text
input slot = 1
stride     = 0x7C = 124 bytes
step rate  = 1
```

The record is exactly 31 dwords.

Bounded destination map:

| Bytes | Captured IA semantic | Captured format | Proven/bounded role |
| --- | --- | --- | --- |
| `0x00..0x0F` | `POSITION1` | `R32G32B32A32_FLOAT` | position offset + local-position scale |
| `0x10..0x1F` | `POSITION2` | `R32G32B32A32_FLOAT` | local X basis; `.w` forwarded |
| `0x20..0x2F` | `POSITION3` | `R32G32B32A32_FLOAT` | transformed-normal dot operand |
| `0x30..0x3F` | `POSITION4` | `R32G32B32A32_FLOAT` | forwarded into PS modulation path |
| `0x40..0x4F` | `POSITION8` | `R32G32B32A32_FLOAT` | local Z basis |
| `0x50..0x5F` | — | no input-layout element covers these bytes in the captured technique | record bytes remain inside the 124-byte stride but are not mapped by the captured IA declaration |
| `0x60..0x63` | `POSITION5` | `R8G8B8A8_UNORM` | normal-derived term multiplier |
| `0x64..0x67` | `POSITION6` | `R8G8B8A8_UNORM` | normal-derived term additive value |
| `0x68..0x6B` | `POSITION7` | `R8G8B8A8_UNORM` | present in IA; unused by the bounded VS |
| `0x6C..0x7B` | `NORMAL1` | `R32G32B32A32_FLOAT` | local Y basis |

“Captured IA semantic” means the D3D11 input-layout semantic observed for the bounded draw. It is a physical shader-interface name, not an original EA business/member name.

The validated control reaches:

```text
DrawIndexedInstanced
```

An exact fixture-scoped runtime/PRECOMP attribution also exists for the maintained instanced control. It remains fixture evidence rather than a family-wide Model Particle default; see `TESTED_EFFECT_FIXTURES.md`.

## Dynamic `013f`

`Dynamic 013f` is a maintained descriptive renderer-family label used for an exact bounded runtime/GPU route.

The executable from the validated build uses per-thread renderer lane/context state on the proven path. At least one authored Ribbon fixture and one authored Particle fixture converge on this renderer family.

Representative controls:

```text
ability_transform_sparkle_trail  -> Ribbon-authored context
gp07_mother_plant_spray          -> Particle-authored context
```

Safe statement:

```text
specific proven Ribbon/Particle fixtures -> Dynamic 013f
```

Unsafe statement:

```text
all RibbonEffect   == Dynamic 013f
all ParticleEffect == Dynamic 013f
```

The broader private meaning of neighboring raw renderer values must not be generalized from representative cases.

## Decal — exact investigated route

The investigated Decal route is mechanically closed end-to-end from the exact runtime renderer input through PRECOMP selection, the direct callback, physical stream binding and `DrawIndexed`.

### Runtime input -> PRECOMP selection

`FUN_140B0C220` supplies exact renderer inputs through:

```text
FUN_140A2A7C0
-> FUN_140A2A8F0
-> FUN_140A25C80 / FUN_140A2C5E0
-> PRECOMP record context + selector
-> FUN_1413689E0
```

Targeted runtime probes establish exact producer -> selected context/selector -> shared-submit identity for the investigated accepted scope.

A narrower selector-1 direct-callback scope resolves:

```text
selector = 1
callback = FUN_140B11640
```

and reaches a resolved PRECOMP pass/shader/state-slice path.

The PRECOMP binary format itself did not change. This proof supplies the runtime join to the already-known structural PRECOMP ABI.

Build-specific record IDs and observation populations are regression evidence rather than renderer constants.

### Physical preparation / draw

Upstream preparation:

```text
0x30-byte draw records
-> FUN_140B10AD0
-> owner+0x448 slot-0 storage, 44 bytes per vertex
-> owner+0x488 slot-1 storage, 4 bytes per companion element
-> owner+0x4C8 index-buffer descriptor
-> FUN_140B0A250 vertex-stream writers
-> FUN_140B09D60 index writer
```

Exact callback:

```text
FUN_140B11640
-> state/binding helper from owner+0x440
-> IA slot 0 from owner+0x448, stride 44
-> IA slot 1 from owner+0x488, stride 4
-> 16-bit index buffer from owner+0x4C8
-> DrawIndexed
```

Safe bounded statement:

```text
investigated Decal route
= 44-byte slot 0 + 4-byte slot 1 + 16-bit index buffer + DrawIndexed
```

This is a route-level fact, not a declaration that every authored `DecalEffect` in every build must use exactly the same renderer layout.

### Historical `16/16` event

A real RenderDoc event with `16/16` bound vertex-buffer strides was captured while `discoball_ground_dots_decal` was active.

Later targeted runtime/static causality tests established that the exact 44/4 callback is not mechanically bridged to that 16/16 activity in the investigated scope.

Therefore the safe classification is:

```text
44/4 + 16-bit IB + DrawIndexed
= mechanically proven investigated Decal route

16/16 RenderDoc event
= real fixture-window GPU observation
= exact Decal ownership not established

44/4 -> 16/16 transformation/bridge
= do not model
```

This does not prove that no other Decal route can exist. Any additional route needs independent authored/runtime ownership and draw-path closure.

## Investigated authored `drawMode = 0x83`

The validated control `ep12_pom_high_skill_fail01_left` does **not** establish a new geometry renderer for authored `drawMode = 0x83`.

Its captured geometry follows the ordinary Model Particle / MLOD route, and the installed MLOD geometry was matched exactly to the captured D3D11 input for that control.

Safe result:

```text
ep12_pom_high_skill_fail01_left
  -> authored drawMode 0x83 is present in the inspected effect graph
  -> captured geometry follows the normal Model Particle/MLOD path
```

Do not generalize a universal business meaning for authored `0x83` from that fixture.

## Sprite / beam route

Authored `SpriteEffect` in the validated build creates a real:

```text
EA::Swarm::cBeamEffect
```

The repeated runtime path reaches a stable renderer-side interface and renderer-record management. The private interface name is not recovered, so the public reference describes the handoff rather than inventing a class identity.

Renderer work may be queued/deferred; a synchronous draw at the exact factory call is not required for the handoff to be valid.

## PRECOMP shader selection

Once a renderer path reaches a shader technique, the validated DX11 PRECOMP structure provides:

```text
outer effect-like record
-> Technique
-> Pass
-> VSRef / PSRef / CSRef
-> render-state slice
```

References are 1-based within their stage table:

```text
0 = absent stage
```

Each validated source record resolves to the exact physical Raw Snappy stream, which decompresses to DXBC. Build-specific shader populations are in `../GAME_VERSION.md`.

See `../precomp/guides/FORMAT_REFERENCE.md` for the binary layout.

## Generic PRECOMP state consumer

Do not use fixture render states to infer generic state semantics.

The common EA PRECOMP consumer recovered from the validated executable provides the generic mapping:

```text
Pass StateStart / StateCount
-> {stateId, rawValue}
-> state dispatcher
-> D3D11 depth/stencil, rasterizer, blend objects/bind parameters
```

Mechanically established groups include:

```text
0x00..0x0F depth/stencil + front/back handling
0x10..0x14 rasterizer
0x15..0x1E render-target-0 blend behavior
```

`0x1B` remains structurally named `BlendState.State0x1B`: exact behavior known, private conceptual name unknown.

Exact converters and float/packed-value mechanics are documented in `../precomp/guides/FORMAT_REFERENCE.md`.

## Fixture pipeline-state snapshots

RenderDoc snapshots are normalized as **fixture regression evidence**.

A snapshot can answer:

```text
what was bound for this exact captured fixture?
```

but cannot by itself answer:

```text
what must every effect in this authored/renderer family use?
```

The manifest keeps fixture state isolated from global routes for exactly this reason.

## Material/state caution

Geometry and shaders are not enough to reproduce the final image. The renderer also uses material/resource identity, textures, samplers, constant data and blend/depth/raster state.

The research proves important dataflow boundaries but deliberately does not rename every renderer-state or material field.

Examples:

- MLOD source records expose an exact `materialReference` field on the proven route;
- MATD/MTST bindings are resolved through exact resource identities and serialized entries rather than a preferred variant heuristic;
- the validated DX11 outer effect-like record `+0x00` is strongly correlated with known MATD Shader hashes but is not promoted to a private EA field name or universal selector rule;
- historical material-field candidates are not used as validated blend-state selectors without consumer/dataflow proof;
- PRECOMP Pass state slices plus the validated common consumer provide the safe state path for the documented build.

A renderer implementation should therefore preserve unknown state/material records and surface diagnostic mismatches instead of substituting an arbitrary shader or blend mode.

## Resource-resolution failures are not renderer semantics

An unresolved/missing model or material target should be reported as that exact resource outcome. It is not evidence for a different renderer family and should not silently fall back to a cube, quad, arbitrary MATD or unrelated texture.

Documented resolution populations are in `../GAME_VERSION.md`.

## Regression rules

A renderer change should be checked against the relevant classes of evidence:

```text
serialized resource graph
fixture IA/input layout
fixture draw kind/count where relevant
exact PRECOMP attribution where available
generic PRECOMP state consumer
fixture pipeline state for regression only
```

Never make a failing fixture disappear by globally ignoring a bone, material, MTST entry, state ID or vertex element.

## Non-generalization checklist

Do not assume:

- one authored family = one renderer;
- one renderer = one shader pair;
- a runtime-observed PRECOMP record is exclusively owned by one named effect;
- the investigated Decal 44/4 route is a universal serialized property of family `0x03`;
- the historical Decal-window `16/16` event belongs to the exact 44/4 route;
- one Model Particle = one static model instance;
- authored `ModelEffect` = Model Particle rendering;
- raw `0x83` or `0x8B` has one universal meaning from one fixture;
- an unknown shader/state field can be named from graphics convention alone;
- no immediate draw means the renderer handoff failed.
