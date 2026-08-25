# VFX renderer reference

Fixture: see `../GAME_VERSION.md`.

This document collects renderer-side facts that are useful to modders and tool authors after an authored VFX has reached a graphics path.

It does **not** define a one-to-one authored-family mapping. Renderer membership must come from actual data/runtime behavior.

For the named effects used as controls for these routes, see `TESTED_EFFECT_FIXTURES.md`.

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

## Model Particle — non-instanced

Validated model geometry resolves through MODL/MLOD data.

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

## Dynamic `013f`

`Dynamic 013f` is a maintained descriptive renderer-family label used for an exact bounded runtime/GPU route.

The executable from the validated build uses per-thread renderer lane/context state on the proven path. At least one authored Ribbon fixture and one authored Particle fixture converge on this renderer family.

Safe statement:

```text
some RibbonEffect and ParticleEffect cases -> Dynamic 013f
```

Unsafe statement:

```text
all RibbonEffect == Dynamic 013f
```

The broader private meaning of neighboring raw renderer values must not be generalized from the representative cases.

## Decal

The investigated Decal route is a dedicated indexed path.

Proven physical preparation includes:

```text
slot 0 vertex stream stride = 44
slot 1 vertex stream stride = 4
index buffer
renderer-state descriptor
DrawIndexed
```

The descriptor is known to participate in renderer-state caching/application, but a private descriptor class/member name is not asserted where the exact name is unavailable.

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

Once a renderer path reaches a shader technique, the validated DX11 PRECOMP fixture provides:

```text
outer effect-like record
-> Technique
-> Pass
-> VSRef / PSRef / CSRef
```

References are 1-based within their stage table:

```text
0 = absent stage
```

Validated corpus:

```text
VS   7,947
PS  30,009
CS       4
```

Each source record resolves to the exact physical Raw Snappy stream, which decompresses to DXBC.

See `../precomp/guides/FORMAT_REFERENCE.md` for the binary layout.

## Material and state caution

Geometry and shaders are not enough to reproduce the final image. The renderer also uses material/resource identity, textures, samplers, constant data and blend/depth/raster state.

The research proves important dataflow boundaries but deliberately does not rename every renderer-state or material field.

Examples:

- MLOD source records expose an exact `materialReference` field on the proven route;
- the modern DX11 outer effect-like record `+0x00` is strongly correlated with known MATD Shader hashes but is not promoted to a current private EA field name;
- Pass state slices are structurally/dataflow bounded, while individual state-pair business names remain conservative.

A renderer implementation should therefore preserve unknown state/material records and surface diagnostic mismatches instead of substituting an arbitrary shader or blend mode.

## Resource-resolution failures are not renderer semantics

Validated Model Particle diagnostics preserve explicit classes:

```text
17,578 model-bit refs
17,478 MODL -> MLOD resolved
72     unresolved IID
20     no model candidate
8      model decode failures
```

An unresolved IID, missing model candidate or model decode failure should be reported as that exact failure. It is not evidence for a different renderer family and should not silently fall back to a cube/quad.

## Non-generalization checklist

Do not assume:

- one authored family = one renderer;
- one renderer = one shader pair;
- one Model Particle = one static model instance;
- authored `ModelEffect` = Model Particle rendering;
- raw `0x83` or `0x8B` has one universal meaning from one fixture;
- an unknown shader/state field can be named from graphics convention alone;
- no immediate draw means the renderer handoff failed.
