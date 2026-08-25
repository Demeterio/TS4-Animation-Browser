# PRECOMP format reference

This is the compact technical reference for the maintained current PRECOMP templates.

For the exact The Sims 4 research build, patch date and PRECOMP fingerprints used by this reference, see:

```text
../../GAME_VERSION.md
```

For VFX/SWARM authored families and the full resource-to-render path, use the sibling modding reference:

```text
../../vfx/
```

Both maintained current PRECOMP template families use **little-endian** decoding.

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

- `Technique` and `Pass` are current structurally/dataflow proven and also have first-party historical PRECOMP vocabulary;
- modern DX11 `Effect` / `Dx11EffectRecord` is a project structural label for the proven outer record;
- `VSRef`, `PSRef`, `CSRef`, `StateStart`, `StateCount` are current dataflow-role labels;
- `StatePairBoundRaw` and `ShaderPreparationRecord` remain descriptive where the private business name is unavailable;
- Win32 Version 2 SPAR words remain `Word0`/`Word1` rather than inheriting unvalidated Version 1 semantics.

---

# DX11 `Shaders_DX11.precomp`

## Validated root

```text
DATA
  -> root entry "shaders"
    -> payload Version 6
```

## Validated fixture populations

| Item | Value |
| --- | ---: |
| payload version | `6` |
| outer effect-like records | `77,771` |
| VS source records | `7,947` |
| PS source records | `30,009` |
| CS source records | `4` |
| shader-preparation records | `3,237` |
| state-pair bound field | `122,417` |

These values are fixture anchors for the corpus identified in `../../GAME_VERSION.md`; they are not universal constants for future game builds.

The state-pair bound value is proven to bound every referenced state slice in the validated corpus. Its finer/private business name is not asserted.

## Record sizes

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

The exposed current graph uses field-relative offsets:

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

## Validated selected fixture

A maintained editor-navigation control resolves:

```text
SelectedEffect             @ 0x001A7D30
SelectedEffectTechniques   @ 0x003BB770
SelectedTechnique          @ 0x003BB770
SelectedPasses             @ 0x00623BE0

TechniqueCount = 6
PassCount      = 1
VSRef          = 5,880
PSRef          = 25,452
CSRef          = 0
StateStart     = 103,303
StateCount     = 1
```

These are validation anchors for the documented fixture, not constants to hard-code into a parser.

## Stage source -> physical shader stream

For all recovered stage source records, source record `+0x00` resolves to the exact physical compact shader stream used by that entry.

Physical ordering in the validated fixture:

```text
Global 0..7946       -> VS  (7,947)
Global 7947..37955   -> PS (30,009)
Global 37956..37959  -> CS      (4)
```

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
- state-pair words into guessed D3D11 business names;
- optional pass-relative target data beyond the proven pointer/sentinel behavior;
- shader-preparation internal fields without independent semantics.

---

# Win32 `Shaders_Win32.precomp`

The current Win32 file is a separate binary format. Do not transplant the DX11 `DATA` layout into it.

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

## Validated fixture populations

| Item | Value |
| --- | ---: |
| SPKG version | `2` |
| PlatformShader records | `27,894` |
| observed vertex shaders | `7,521` |
| observed pixel shaders | `20,373` |
| DBUF 32-bit values | `1,994,786` |
| SPAR records | `661` |

Fixture cross-check offsets:

```text
DBUF @ 0x02799D6A
SPAR @ 0x02F35DFA
SMDB @ 0x02F372AA
```

These offsets are not universal constants.

## Variable-sized `PlatformShader`

Current exposed layout:

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

The names above have preserved first-party Version 1 vocabulary behind them while their current Version 2 layout/mechanics are independently revalidated.

## DBUF / SPAR

```text
DBUF
  -> DefaultsSize
  -> uint32 DefaultData[DefaultsSize]

SPAR
  -> ShaderParameterCount
  -> ShaderParameterRecord[ShaderParameterCount]
```

The current exposed Version 2 SPAR record is `0x08` bytes:

```text
uint32 Word0
uint32 Word1
```

The richer names from historical Version 1 are not promoted without current Version 2 proof.

The maintained current templates stop at `SMDB`.

Historical tags such as:

```text
SETS SSET SHDS SBLK TECH PASS PARM KNM END
```

belong to historical reference material until their current Version 2 layout is independently revalidated.

---

# Editor-template boundary

The maintained files are:

```text
../templates/010-editor/*.bt
../templates/imhex/*.hexpat
```

Their role is **structured inspection**, not complete shader-corpus decoding.

The DX11 patterns expose the validated tables/pointers/counts and selected Effect-like/Technique/Pass navigation. The dedicated research/parser tooling performed the corpus-wide Raw Snappy decompression and DXBC validation.

The current VFX/SWARM closure does not change these PRECOMP record layouts. The template parser bodies should therefore not be edited simply to mirror new higher-level VFX documentation.

---

# How PRECOMP fits the VFX renderer

PRECOMP is downstream of authored VFX data.

A simplified path is:

```text
VisualEffects / SWARM
-> runtime simulation/orchestration
-> renderer/material/resource selection
-> PRECOMP outer effect-like record
-> Technique
-> Pass
-> stage refs
-> Raw Snappy -> DXBC
-> D3D11 shader binding
-> draw
```

Representative current renderer facts that connect to this stage include:

```text
Classic vertex stride         24 bytes
Model Particle MLOD source    materialReference + VRTF/VBUF/IBUF
Instanced Model Particle      slot 1, stride 124, DrawIndexedInstanced
Decal                         indexed dedicated path
Dynamic 013f                  bounded renderer family
```

Full details:

```text
../../vfx/PACKAGE_TO_RENDER.md
../../vfx/RENDERER_REFERENCE.md
```

---

# Current VFX resource context

The current executable maps:

```text
VisualEffects            0x1B192049 -> Sims4EffectsOpt  -> .swb2
VisualEffectsInstanceMap 0x1B19204A -> Sims4EffectsOptH -> .swh2
VisualEffectsMerged      0xEA5118B0 -> Sims4Effects     -> .swb
```

The current merged library identities are completely resolved through the InstanceMap to existing split resources in the validated corpus. Exact build-specific counts belong to `../../GAME_VERSION.md`.

This is a resource-library fact; it does not change the binary layout of `Shaders_DX11.precomp`.

---

# Compatibility boundary

The canonical game build, patch date and exact PRECOMP fingerprints for this reference are maintained in:

```text
../../GAME_VERSION.md
```

A changed hash means the file is different. Revalidate the affected version/count/stride/pointer/source-stream boundaries before claiming the same completeness on another build.

Do not weaken a parser warning simply to make a changed file appear supported.
