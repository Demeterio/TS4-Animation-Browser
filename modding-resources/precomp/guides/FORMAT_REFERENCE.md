# PRECOMP format reference

This is the technical reference for the maintained validated PRECOMP templates and the proven DX11 state consumer.

For the exact The Sims 4 research build, patch date, corpus populations and PRECOMP fingerprints used by this reference, see:

```text
../../GAME_VERSION.md
```

For VFX/SWARM authored families and the full resource-to-render path, use:

```text
../../vfx/
```

Build-specific runtime/PRECOMP statements on this page refer to the documented fixture, not automatically to the latest live game patch.

Both maintained PRECOMP template families use **little-endian** decoding.

## Naming rule

A proven structure does not require an invented private EA name.

Use `LABEL_PROVENANCE.md` to distinguish:

```text
EA_NAME_PROVEN
DATAFLOW_PROVEN
PHYSICAL_SEMANTIC_PROVEN
first-party historical vocabulary
historical/community vocabulary
project structural/descriptive labels
UNKNOWN_BOUNDED
```

Important examples:

- `Technique` and `Pass` are structurally/dataflow proven for the documented fixture and also have first-party historical PRECOMP vocabulary;
- modern DX11 `Effect` / `Dx11EffectRecord` is a project structural label for the proven outer record;
- `VSRef`, `PSRef`, `CSRef`, `StateStart`, `StateCount` are dataflow-role labels;
- `StateId` / `Value` are structural labels for the serialized state-pair words; their D3D destinations are established separately by the common consumer;
- `ShaderPreparationRecord` remains descriptive where the private business name is unavailable;
- Win32 Version 2 SPAR words remain `Word0`/`Word1` rather than inheriting unvalidated Version 1 semantics.

---

# DX11 `Shaders_DX11.precomp`

## Validated root

```text
DATA
  -> root entry "shaders"
    -> payload Version 6
```

Build-specific table populations and file fingerprints are intentionally centralized in `../../GAME_VERSION.md`.

## Record sizes

These are structural format facts for the maintained DX11 boundary:

| Record | Size |
| --- | ---: |
| outer effect-like record | `0x1C` |
| Technique | `0x0C` |
| Pass | `0x18` |
| VS source record | `0x34` |
| PS source record | `0x2C` |
| CS source record | `0x2C` |
| state pair | `0x08` |
| shader-preparation record | `0x1C` |

## Relative pointers

The exposed validated graph uses field-relative offsets:

```text
target = address_of(relative_field) + relative_value
```

The observed optional pass-relative value uses:

```text
INT_MIN = absent
```

Do not treat fixture addresses as universal offsets in a later PRECOMP.

## Technique layout

```text
+0x00  Unknown00
+0x04  PassesRelative
+0x08  PassCount
```

## Pass layout

```text
+0x00  VSRef
+0x04  PSRef
+0x08  CSRef
+0x0C  StateStart
+0x10  StateCount
+0x14  OptionalRelative
```

Stage-reference rule:

```text
0     = no shader for the stage
N > 0 = 1-based index into the corresponding stage source table
```

The editor templates use this hierarchy for navigation. Build-specific selected-record examples belong to fixture/regression documentation rather than the generic format definition.

## Stage source -> physical shader stream

For all recovered stage source records in the documented fixture, source record `+0x00` resolves to the exact physical compact shader stream used by that entry.

Storage/decoding:

```text
physical compact stream
-> Raw Snappy decompression
-> DXBC
```

Every decoded payload in the documented corpus was validated as a usable DXBC shader through the corresponding Direct3D inspection/creation path.

The original private name for source record `+0x00` is not asserted.

## Important DX11 non-claims

The maintained templates do **not** rename:

- outer record `+0x00` solely from its strong correlation with known MATD Shader hashes;
- unknown neighboring stage-source fields;
- serialized state-pair words into guessed private EA member names;
- optional pass-relative target data beyond the proven pointer/sentinel behavior;
- shader-preparation internal fields without independent semantics.

---

# DX11 state-pair structure and consumer

## Serialized state pair

Each serialized pair is:

```text
0x08  bytes
+0x00 uint32 stateId
+0x04 uint32 rawValue
```

A Pass selects a slice with `StateStart` / `StateCount`.

The private source-level member names of the two words are not claimed. `stateId` and `rawValue` describe the structure, while the consumer proves what supported IDs do.

## Common state consumer

The common consumer recovered from the validated executable is:

```text
PRECOMP Pass
-> StateStart / StateCount
-> serialized {stateId, rawValue}
-> common cached setter
-> common state dispatcher
-> D3D11 state objects / bind parameters
```

The dispatcher handles IDs `0x00..0x1E`. IDs above `0x1E` reach the common epilogue without a state write in this path.

This mapping was recovered from the common EA consumer. It is **not** inferred from matching a handful of RenderDoc states.

## State-ID catalog

| ID | Mechanical destination/role | Status | Raw-value mechanics |
| --- | --- | --- | --- |
| `0x00` | depth enable | `DATAFLOW_PROVEN` | non-zero boolean |
| `0x01` | depth write mask | `DATAFLOW_PROVEN` | non-zero -> enabled write mask |
| `0x02` | depth comparison func | `DATAFLOW_PROVEN` | `1..7` unchanged, other -> `8` |
| `0x03` | stencil enable | `DATAFLOW_PROVEN` | non-zero boolean |
| `0x04` | front stencil fail op | `DATAFLOW_PROVEN` | `2..8` unchanged, other -> `1` |
| `0x05` | front stencil depth-fail op | `DATAFLOW_PROVEN` | same stencil translator |
| `0x06` | front stencil pass op | `DATAFLOW_PROVEN` | same stencil translator |
| `0x07` | front stencil comparison func | `DATAFLOW_PROVEN` | same comparison translator as `0x02` |
| `0x08` | stencil reference | `DATAFLOW_PROVEN` | low byte; submitted separately |
| `0x09` | stencil read mask | `DATAFLOW_PROVEN` | low byte |
| `0x0A` | stencil write mask | `DATAFLOW_PROVEN` | low byte |
| `0x0B` | back stencil fail op | `DATAFLOW_PROVEN` | same stencil translator |
| `0x0C` | back stencil depth-fail op | `DATAFLOW_PROVEN` | same stencil translator |
| `0x0D` | back stencil pass op | `DATAFLOW_PROVEN` | same stencil translator |
| `0x0E` | back stencil comparison func | `DATAFLOW_PROVEN` | same comparison translator as `0x02` |
| `0x0F` | `DepthStencil.State0x0F` | `DATAFLOW_PROVEN` | when false, copy front-face operations/function to back-face fields before state creation |
| `0x10` | rasterizer fill mode | `DATAFLOW_PROVEN` | raw `2` -> wireframe, otherwise solid |
| `0x11` | rasterizer cull mode | `DATAFLOW_PROVEN` | `1` none, `3` back, otherwise front |
| `0x12` | rasterizer scissor enable | `DATAFLOW_PROVEN` | non-zero boolean |
| `0x13` | rasterizer slope-scaled depth bias | `DATAFLOW_PROVEN` | raw uint32 bits reinterpreted directly as float32 |
| `0x14` | rasterizer integer depth bias | `DATAFLOW_PROVEN` | raw bits -> float32 -> context-dependent scaling -> truncating int32 conversion |
| `0x15` | render-target-0 blend enable | `DATAFLOW_PROVEN` | non-zero boolean |
| `0x16` | RT0 source blend factor | `DATAFLOW_PROVEN` | `2..11,14,15` unchanged, other -> `1` |
| `0x17` | RT0 destination blend factor | `DATAFLOW_PROVEN` | same blend-factor translator |
| `0x18` | RT0 write mask | `DATAFLOW_PROVEN` | low byte |
| `0x19` | RT0 blend op | `DATAFLOW_PROVEN` | `2..5` unchanged, other -> `1` |
| `0x1A` | four-component blend factor | `DATAFLOW_PROVEN` | packed bytes -> float32 × exact float `1/255` rounding |
| `0x1B` | `BlendState.State0x1B` | `UNKNOWN_BOUNDED` private purpose; mechanics proven | exact blend-descriptor transform described below |
| `0x1C` | RT0 source alpha blend factor | `DATAFLOW_PROVEN` | same blend-factor translator |
| `0x1D` | RT0 destination alpha blend factor | `DATAFLOW_PROVEN` | same blend-factor translator |
| `0x1E` | RT0 alpha blend op | `DATAFLOW_PROVEN` | same blend-op translator |

The role names above are public D3D/mechanical descriptions derived from destination structures/dataflow. They are **not** claims that EA used those exact private identifiers.

## Exact enum translators

### Comparison

```text
raw 1..7 -> same D3D11 numeric enum
other    -> 8
```

### Stencil operation

```text
raw 2..8 -> same D3D11 numeric enum
other    -> 1
```

### Blend factor

```text
raw 2..11, 14, 15 -> same D3D11 numeric enum
other             -> 1
```

### Blend operation

```text
raw 2..5 -> same D3D11 numeric enum
other    -> 1
```

These are proven consumer conversions. They are not inferred by matching RenderDoc output.

## Exact float/value mechanics

### State `0x13`

```text
raw uint32 bits
-> direct float32 reinterpretation
-> slope-scaled depth-bias destination
```

There is no integer-to-float numerical conversion.

### State `0x14`

The incoming bits are interpreted as float32 and multiplied according to a runtime context byte:

```text
0x29        × 32768.0
0x63        × 16777216.0
0x8D..0x92  × 8388608.0
0x93        × 16777216.0
other/null  × 1.0
```

The result is converted to signed int32 with truncation toward zero.

The private meaning of those context-byte values remains intentionally unassigned.

### State `0x1A`

Four packed bytes are extracted low-to-high and converted to float components using exact constant bits:

```text
0x3B808081
```

This is the float32 rounding of `1 / 255`.

### State `0x1B`

Canonical structural key:

```text
BlendState.State0x1B
```

Mechanically:

```text
value != 0
    use supplied 0x108-byte blend descriptor as-is

value == 0
    copy descriptor
    rewrite RT0 blend-factor fields:
      3  -> 5
      4  -> 6
      9  -> 7
      10 -> 8
    hash/create/cache rewritten descriptor
```

The rewrites move public D3D blend-factor values to their alpha counterparts. The private business purpose/name of the controlling state remains `UNKNOWN_BOUNDED`.

Build-specific closure percentages for this dispatcher are kept in `../../GAME_VERSION.md` rather than duplicated here.

---

# Outer effect-like record

Validated DX11 size: `0x1C`.

The first word at `+0x00` is strongly correlated with known MATD shader hashes, including an exact equality on a maintained fixture. This remains a physical correlation, not a proven universal `MATD.shader_key -> PRECOMP record` selector relation.

The validated runtime resolver adds an important but narrower causal result: a 32-bit value read from a renderer-side resource/material object at runtime offset `+0x4C` is passed into the PRECOMP resource/context lookup chain. The lookup then participates in resolving the context supplied to the common PRECOMP dispatcher.

What is proven:

```text
runtime object+0x4C
-> resource lookup
-> variant/state discriminator
-> resolved PRECOMP context
-> context + selector
-> common PRECOMP dispatch
```

The following relationships are **`UNKNOWN_BOUNDED` / not established for the documented fixture**:

```text
serialized MATD Shader field -> runtime object+0x4C assignment
runtime object+0x4C == PRECOMP outer +0x00 universally
```

These are non-blocking evidence boundaries, not promises that a private selector relation still needs to be found. Reopen them only if a future implementation requirement, contradictory fixture or changed game build needs the missing edge.

The common transient path also has a no-resource/default branch that constructs contexts from first-party string hashes observed in the validated build, such as `particle` / `ParticleLight`. This is another reason not to model the whole PRECOMP resolver as a direct MATD-key table.

Therefore maintained templates keep outer `+0x00` structural/raw instead of assigning a private EA material-key name.

## Shader-preparation record

Validated exposed size: `0x1C`.

The table/record boundary is structural. Internal private fields remain raw until independently established.

---

# How PRECOMP connects to a runtime VFX route

PRECOMP is downstream of authored VFX data.

The generic relationship is:

```text
VisualEffects / SWARM
-> runtime simulation/orchestration
-> renderer/material/resource selection
-> runtime PRECOMP context/selector selection when applicable
-> PRECOMP outer effect-like record
-> Technique
-> Pass
-> stage refs + state slice
-> Raw Snappy -> DXBC
-> D3D11 shader/resource/state binding
-> draw
```

Structural PRECOMP decoding by itself does **not** prove which PRECOMP record/pass a named runtime VFX selects. That attribution requires separate runtime/dataflow proof.

Named VFX/PRECOMP cardinality is therefore not one-to-one:

```text
one named VFX -> zero PRECOMP selections
one named VFX -> one bounded selection
one named VFX -> several selections across branches/routes
several named VFX -> shared PRECOMP context/record
```

A PRECOMP-compatible shader pair, state slice or outer record is not exclusive named-effect ownership without the runtime attribution edge.

## Common transient context/selector bridge

For the validated common transient route, the producer-to-dispatch mechanics are exact.

The `0xA0` transient record carries:

```text
record +0x18  context candidate A
record +0x20  context candidate B
record +0x28  uint32 selector
```

The downstream bridge selects `+0x20` only when an exact runtime flag/condition path permits it and the field is non-zero; otherwise it selects `+0x18`. The private business meaning of that condition is not asserted.

The selected pair is copied to per-thread dispatch storage and then consumed by the common dispatcher:

```text
selected context -> per-thread context slot
record +0x28     -> per-thread selector slot
-> FUN_1413689E0(context, selector, ...)
```

This is `DATAFLOW_PROVEN` behavior for the documented build. It still does not assign one universal PRECOMP record to an authored family or named effect.

## SceneModel context/selector bridge

The validated SceneModel path independently proves the same high-level contract:

```text
context = explicit record context
       OR table-selected owner context slot / fallback

selector = record selector when non-negative
        OR owner-side selector fallback

-> FUN_1413689E0(context, selector, ...)
```

The private meanings of the owner slots/table bytes remain structural. Their mechanics are sufficient to reproduce the selection path without inventing names.

Representative renderer facts that connect to this stage include Classic dynamic geometry, Model Particle geometry, instanced Model Particle submission, Dynamic `013f` and the investigated Decal route. Their physical layouts belong in `../../vfx/RENDERER_REFERENCE.md`, not in the generic PRECOMP format.

## Investigated Decal runtime attribution

For the investigated Decal route, the separate runtime join has been established:

```text
runtime Decal owner/input
-> PRECOMP producer/context + selector
-> selected effect-like record / Technique / Pass
-> stage refs + state slice
-> exact Decal callback
-> indexed D3D11 draw
```

The exact callback uses the already-proven Decal physical route with a 44-byte slot 0, 4-byte slot 1 and 16-bit index buffer.

This result means:

```text
runtime Decal route -> existing PRECOMP ABI = PROVEN for the investigated route
```

It does **not** mean:

```text
PRECOMP binary format changed
all authored DecalEffect records universally use one fixed record set
those PRECOMP records are exclusively owned by one named fixture
all VFX renderer families now have an equivalent exact effect->record attribution
```

A separate historical RenderDoc event with 16/16 bound strides was captured while the Decal fixture was active. Targeted causality/parent-scope tests do not establish ownership by the exact 44/4 route, so no 44/4-to-16/16 transformation should be modeled.

Full public route detail:

```text
../../vfx/PACKAGE_TO_RENDER.md
../../vfx/RENDERER_REFERENCE.md
../../vfx/TESTED_EFFECT_FIXTURES.md
```

---

# Win32 `Shaders_Win32.precomp`

The validated Win32 file is a separate binary format. Do not transplant the DX11 `DATA` layout into it.

## Validated Version 2 prefix

```text
SPKG
  -> SHDB
  -> LINK
  -> PlatformShader[]
  -> DBUF
  -> SPAR
  -> SMDB
```

Build-specific populations and fixture offsets are kept in `../../GAME_VERSION.md` or internal validation evidence rather than copied into this generic format reference.

## Variable-sized `PlatformShader`

Validated exposed layout:

```text
u8   IsPixelShader
u16  ShaderPlatformMask
u32  ShaderSize
char ShaderTag[4]
u32  ByteCodeSize
u8   ByteCode[ByteCodeSize]
```

Because `ByteCodeSize` changes per record, `PlatformShader` is variable-sized.

The maintained 010 Editor template therefore intentionally uses a non-optimized array for this table. Removing that behavior causes incorrect navigation because the editor otherwise treats records as fixed-size.

The names above preserve first-party Version 1 vocabulary where the validated Version 2 layout/mechanics are independently revalidated.

## DBUF / SPAR

```text
DBUF
  -> DefaultsSize
  -> uint32 DefaultData[DefaultsSize]

SPAR
  -> ShaderParameterCount
  -> ShaderParameterRecord[ShaderParameterCount]
```

The validated exposed Version 2 SPAR record is `0x08` bytes:

```text
uint32 Word0
uint32 Word1
```

The richer names from historical Version 1 are not promoted without validated Version 2 proof.

The maintained templates stop at `SMDB`.

Historical tags such as:

```text
SETS SSET SHDS SBLK TECH PASS PARM KNM END
```

belong to historical reference material until their Version 2 layout is independently revalidated.

---

# Editor-template boundary

The maintained files are:

```text
../templates/010-editor/*.bt
../templates/imhex/*.hexpat
```

Their role is **structured inspection**, not complete shader-corpus decoding.

The DX11 patterns expose the validated tables/pointers/counts and Effect-like/Technique/Pass navigation. Dedicated research/parser tooling performed corpus-wide Raw Snappy decompression and DXBC validation.

The editor templates intentionally do not embed another Snappy decoder. Human inspection and exhaustive decompression/validation are different jobs.

Runtime VFX research can prove new joins **to** PRECOMP without changing PRECOMP itself. Edit a template only when the binary structure, count, pointer rule or field behavior in the PRECOMP file has independently changed or been re-proven.

The common runtime resolver improved upstream selection mechanics; it did not change the serialized outer record, Technique/Pass layout, shader tables or state-pair ABI.

The generic state-consumer work improved downstream semantics; it did not change the serialized 8-byte pair, Pass layout or table pointer rules.

---

# Validated VFX resource context

The validated executable maps:

```text
VisualEffects            0x1B192049 -> Sims4EffectsOpt  -> .swb2
VisualEffectsInstanceMap 0x1B19204A -> Sims4EffectsOptH -> .swh2
VisualEffectsMerged      0xEA5118B0 -> Sims4Effects     -> .swb
```

The merged/split identity relationship and build-specific corpus counts are documented in `../../vfx/RESOURCE_TOPOLOGY.md` and `../../GAME_VERSION.md`.

This is a resource-library fact; it does not change the binary layout of `Shaders_DX11.precomp`.

---

# Compatibility boundary

The canonical game build, patch date, corpus anchors and exact PRECOMP fingerprints for this reference are maintained in:

```text
../../GAME_VERSION.md
```

A changed hash means the file is different. Revalidate the affected version/count/stride/pointer/source-stream boundaries before claiming the same completeness on another build.

If the executable changed, revalidate consumer/dataflow claims such as the state dispatcher and common runtime context/selector resolver independently from the serialized PRECOMP layout.

Do not weaken a parser warning simply to make a changed file appear supported.
