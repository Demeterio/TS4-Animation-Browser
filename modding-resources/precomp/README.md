# The Sims 4 PRECOMP resources

This folder contains maintained binary templates and technical documentation for shader PRECOMP files used by The Sims 4.

These are **inspection/research resources** for modders and tool authors. They are not required to run TS4 Animation Browser and they do not belong in the game's `Mods` folder.

If shaders, PRECOMP files or binary templates are new to you, start with the project-wide guide:

```text
../BEGINNER_GUIDE.md
```

For the exact The Sims 4 research build, patch date, corpus populations and PRECOMP fingerprints, always use:

```text
../GAME_VERSION.md
```

For the complete VFX path from a DBPF package to a D3D11 draw:

```text
../vfx/PACKAGE_TO_RENDER.md
```

For project-wide attribution/disclaimer information:

```text
../README.md
```

Build-specific runtime/PRECOMP statements on this page refer to the validated fixture in `../GAME_VERSION.md`; they are not claims about the latest live game patch unless another build is explicitly named.

## `.precomp`, `.bt` and `.hexpat` are different things

```text
EA .precomp file = binary game data used by The Sims 4
010 Editor .bt   = text Binary Template used to inspect that data
ImHex .hexpat    = text Pattern Language file used to inspect that data
```

The maintained templates do not contain or replace EA's installed PRECOMP binaries.

Use a working copy of your own installed files:

```text
The Sims 4\Game\Bin\res\Shaders_DX11.precomp
The Sims 4\Game\Bin\res\Shaders_Win32.precomp
```

Do not edit the only copy inside the game installation while learning.

## Folder layout

```text
precomp/
├─ README.md
├─ guides/
│  ├─ FORMAT_REFERENCE.md
│  ├─ LABEL_PROVENANCE.md
│  ├─ LEGACY_TEMPLATES.md
│  └─ TOOLS_AND_METHOD.md
├─ old-references/
└─ templates/
   ├─ 010-editor/
   └─ imhex/
```

## Modern DX11 PRECOMP

Game file:

```text
Shaders_DX11.precomp
```

Validated DX11 root:

```text
DATA
  -> root entry "shaders"
    -> payload Version 6
```

Validated hierarchy:

```text
outer effect-like record
  -> Technique
    -> Pass
      -> 1-based VS / PS / CS source reference
        -> stage source record
          -> exact physical Raw Snappy stream
            -> DXBC
```

Reference rule:

```text
0     = no shader for that stage
N > 0 = 1-based index into the relevant stage table
```

The outer effect-like record is a PRECOMP structural object, **not a named-VFX identity**. Runtime attribution is a separate layer. Depending on the effect graph and runtime route, a named VFX can reach no PRECOMP selection, one bounded selection, or multiple selections; several named effects can also share a PRECOMP context/record. Do not infer exclusive ownership from structural compatibility, shader hashes or record proximity alone.

The exact validated populations and PRECOMP file fingerprints are centralized in `../GAME_VERSION.md` rather than copied here.

Source-record-to-stream mapping is mechanically closed for the documented fixture across all VS/PS/CS records. All compact streams in that fixture were reconstructed as Raw Snappy -> DXBC and validated through Direct3D reflection/disassembly and matching D3D11 shader-creation paths.

The editor templates intentionally **do not embed another Snappy decoder**. They expose the validated binary structure for human inspection; exhaustive decompression/validation belongs in dedicated parser tooling.

## Generic DX11 render-state consumer

A Pass also selects a serialized state slice through `StateStart` / `StateCount`.

The validated executable establishes the generic path:

```text
Pass state slice
-> {stateId, rawValue}
-> common cached setter
-> common state dispatcher
-> D3D11 depth/stencil, rasterizer and blend state/bind parameters
```

The dispatcher mechanically handles state IDs `0x00..0x1E`. Public role descriptions such as depth enable, cull mode or blend factor come from the validated consumer dataflow into D3D11 structures, not from guessing values in a fixture capture.

State `0x1B` is the important naming boundary: its descriptor transformation is exact, but its private conceptual EA name remains unknown. The maintained documentation therefore keeps the structural label `BlendState.State0x1B`.

See `guides/FORMAT_REFERENCE.md` for the state-ID table and exact conversions.

## Naming boundary for DX11

The physical structure can be exact even when the stripped private C++ name is unknown.

Important validated labels:

- `Technique` and `Pass` are structurally/dataflow proven for the documented fixture and also occur in preserved first-party historical PRECOMP vocabulary;
- `Effect` / `Dx11EffectRecord` is a maintained structural label for the validated outer record, not a claim about its stripped EA private type name;
- `VSRef`, `PSRef`, `CSRef`, `StateStart`, `StateCount` are dataflow-role labels;
- source-record `+0x00` -> exact physical shader stream is proven, but the original private member name is not asserted;
- `StateId` / `Value` remain structural pair-member labels even though the common consumer establishes the D3D11 destination of each supported state ID;
- unknown neighboring values remain `Unknown`, `Raw`, `WordN` or equivalent.

See:

```text
guides/LABEL_PROVENANCE.md
```

The beginner explanation of why exact behavior and exact original EA naming are different problems is in:

```text
../BEGINNER_GUIDE.md
```

## Win32 Version 2 PRECOMP

`Shaders_Win32.precomp` is a separate format and must not be treated as the DX11 `DATA` layout.

The maintained validated publication boundary is:

```text
SPKG
  -> SHDB
  -> LINK
  -> PlatformShader[]
  -> DBUF
  -> SPAR
  -> SMDB
```

The exact validated population is recorded with the research fixture in `../GAME_VERSION.md` and the structural details remain in `guides/FORMAT_REFERENCE.md`.

The maintained templates intentionally stop at the mechanically revalidated Version 2 boundary. The larger historical Version 1 `TECH/PASS/PARM` tail is not copied into Version 2 merely because an old first-party template contains it.

Historical Version 1 names are reused only when the validated Version 2 bytes/mechanics independently support the transfer. Version 2 SPAR words remain `Word0`/`Word1` where the old richer semantics have not been revalidated.

## Maintained editor templates

### 010 Editor

```text
templates/010-editor/
```

Contains `.bt` Binary Templates for the validated DX11 and Win32 Version 2 boundaries.

Both templates were manually runtime-tested against the documented research fixtures. The Win32 template intentionally disables fixed-size array optimization for variable-sized `PlatformShader` records.

The `.bt` parser bodies are not changed merely because later VFX research adds more package-to-render context. A PRECOMP template is extended only when new PRECOMP bytes/layout/dataflow justify a structural change.

### ImHex

```text
templates/imhex/
```

Contains native `.hexpat` Pattern Language ports of the same maintained publication boundaries.

Both patterns were also manually executed against the documented fixtures and reached their expected boundaries without Pattern Language runtime errors.

ImHex is a free/open-source alternative; it does not execute 010 Editor `.bt` files directly.

## Validated DX11 record sizes

These are structural format facts, not corpus populations:

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

Validated field-relative pointer rule for the exposed graph:

```text
target = address_of(relative_field) + relative_value
```

The observed optional pass-relative field uses `INT_MIN` as an absent sentinel.

Detailed binary layout:

```text
guides/FORMAT_REFERENCE.md
```

## PRECOMP is only one part of rendering

The PRECOMP tells the renderer how to reach compiled shader programs and related pass/state data. It is not the entire VFX.

A complete VFX can additionally depend on:

```text
DBPF resource identity
SWARM authored family data
runtime simulation/orchestration
MODL/MLOD geometry
VRTF/VBUF/IBUF
material references
textures/samplers
constant data
blend/depth/raster state
D3D11 input layout and buffers
```

The maintained modder-facing pipeline documentation is in:

```text
../vfx/
```

## Compatibility boundary

The exact Sims 4 build/date, corpus anchors and PRECOMP hashes used for validation are maintained in one place:

```text
../GAME_VERSION.md
```

A later/different hash means the bytes are not identical to the fixture. It does **not** automatically prove the format changed, and it does not automatically extend the validation claim either.

For a later game update, minimally recheck:

1. file signatures, versions, sizes and hashes;
2. table counts/strides and relative-pointer boundaries;
3. pass stage-reference rules;
4. source-record -> physical stream mapping if the DX11 shader package changed;
5. Raw Snappy -> DXBC reconstruction for the changed corpus;
6. state-consumer behavior if the executable changed;
7. only reopen semantic labels when new bytes/dataflow contradict the validated model.

## Historical references

Historical TS4/TS3 templates are preserved under:

```text
old-references/
```

They remain historical evidence and should not be rewritten to match the validated format.

Read:

```text
guides/LEGACY_TEMPLATES.md
old-references/README.md
```

## Documentation index

- `../BEGINNER_GUIDE.md` — beginner explanation of VFX, SWARM, shaders, PRECOMP, binary templates and EA-name recovery;
- `../GAME_VERSION.md` — canonical game build/date, corpus anchors, closure metrics and PRECOMP fingerprints;
- `guides/FORMAT_REFERENCE.md` — validated PRECOMP binary/state reference;
- `guides/LABEL_PROVENANCE.md` — exact provenance/confidence of public labels;
- `guides/LEGACY_TEMPLATES.md` — validated vs historical template boundaries;
- `guides/TOOLS_AND_METHOD.md` — tools/evidence methodology;
- `../vfx/PACKAGE_TO_RENDER.md` — complete VFX resource-to-D3D11 path;
- `../vfx/AUTHORED_FAMILIES.md` — modern authored-family reference;
- `../vfx/RENDERER_REFERENCE.md` — renderer/geometry/GPU reference;
- `../vfx/RESOURCE_TOPOLOGY.md` — `.swb/.swb2/.swh2` identity and runtime topology;
- `../CHANGELOG.md` — project-wide public modding-resource history.

## Safety and redistribution

Do not add extracted EA PRECOMP binaries, extracted DXBC corpora, executable fragments or other game assets to this resource set.

The maintained templates are descriptions of validated binary structure. Historical/third-party files under `old-references/` retain their own provenance and rights information.
