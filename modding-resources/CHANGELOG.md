# Modding resources changelog

This changelog records changes to the public `modding-resources/` documentation, PRECOMP templates and VFX/SWARM reference material.

The canonical The Sims 4 research build, patch date and PRECOMP fingerprints are maintained in:

```text
GAME_VERSION.md
```

Do not copy a new game-version claim into this changelog without updating that canonical fixture file first.

## 2026-08-24 — VFX/SWARM pipeline documentation and fixture centralization

### VFX resource topology

- Added a dedicated public `vfx/` documentation set instead of mixing authored VFX/runtime/renderer facts into PRECOMP-only documentation.
- Documented the three VFX DBPF resource types used by the validated build:
  - `0x1B192049` — `VisualEffects`;
  - `0x1B19204A` — `VisualEffectsInstanceMap`;
  - `0xEA5118B0` — `VisualEffectsMerged`.
- Documented the validated EA resource-name/extension mapping:

```text
VisualEffects            -> Sims4EffectsOpt  -> .swb2
VisualEffectsInstanceMap -> Sims4EffectsOptH -> .swh2
VisualEffectsMerged      -> Sims4Effects     -> .swb
```

- Documented the exact serialized identity route:

```text
Effect IID -> MasterIID -> effective split VisualEffects resource
```

- Recorded the complete merged/split identity audit for the validated corpus: every validated merged effect identity resolves through the InstanceMap to an existing split `VisualEffects` resource.
- Documented that `MasterIID` does not have to equal `Effect IID`; several effect identities may belong to the same split resource.
- Recorded the remaining 18-byte InstanceMap trailer as `UNKNOWN_BOUNDED` rather than assigning a speculative field name.

### Executable merged/split selection

- Documented executable mappings from the validated build for `Sims4EffectsOpt`, `Sims4EffectsOptH` and `Sims4Effects`.
- Documented the `SwarmDisableCollectionStreaming` configuration branch observed in that build:

```text
false -> collection-streaming / split + InstanceMap-side route
true  -> VisualEffectsMerged / .swb route
```

- Clarified that the merged library is not dead data in the validated executable while also not being required to gain additional identity coverage in the validated corpus.

### Authored VFX/SWARM families

- Added `vfx/AUTHORED_FAMILIES.md` with the complete validated modern authored-family inventory and bounded runtime role of each active family.
- Documented the 11 active authored families:

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

- Explicitly separated:

```text
authored SWARM family
!= CPU simulation/orchestration path
!= GPU renderer family
!= shader
```

- Documented zero-population families separately for the validated modern corpus and clarified that authored `ModelEffect` is not the same concept as the observed Model Particle renderer.
- Added viewer/tool design guidance for renderer-facing, child-orchestration, sound, shake and context-dependent families.
- Added `vfx/TESTED_EFFECT_FIXTURES.md` so the named controls, runtime observations, renderer captures and identity examples behind the maintained research are visible in one place without confusing automated corpus coverage with manual fixture coverage.

### Package-to-render chain

- Added `vfx/PACKAGE_TO_RENDER.md` as the modder-facing end-to-end explanation of the validated bounded pipeline:

```text
DBPF package
-> effective VFX resource
-> VisualEffect identity/definition
-> authored family data
-> runtime Description / Effect / child orchestration
-> simulation or non-GPU endpoint
-> renderer family when graphics are required
-> geometry/material/texture resolution
-> PRECOMP outer record -> Technique -> Pass
-> VS / PS / CS
-> D3D11 bindings/state
-> Draw / DrawIndexed / DrawIndexedInstanced
```

- Added concrete examples covering Classic particles, Model Particles, Ribbon/Dynamic behavior and effects combining sound with visual behavior.

### Renderer reference

- Added `vfx/RENDERER_REFERENCE.md` with reusable bounded renderer facts, including:
  - Classic particle 24-byte vertex stride;
  - Model Particle MODL/MLOD resolution;
  - MLOD `0x206` source-record references to material/VRTF/VBUF/IBUF;
  - zero-VRTF fallback behavior;
  - instanced Model Particle slot-1 124-byte record and `DrawIndexedInstanced` path;
  - Dynamic `013f` bounded route;
  - Decal route;
  - the investigated `ep12_pom_high_skill_fail01_left` authored `drawMode = 0x83` case;
  - Sprite/beam runtime handoff;
  - PRECOMP/D3D11 bridge.

### PRECOMP documentation integration

- Reworked the PRECOMP documentation so `.precomp`, `.bt` and `.hexpat` are consistently described as three different things.
- Kept the maintained `.bt` and `.hexpat` parser bodies unchanged where the VFX findings did not alter any proven PRECOMP offset, stride, pointer rule or shader-reference mechanic.
- Added `precomp/guides/LABEL_PROVENANCE.md` to distinguish validated first-party names, proven dataflow/physical roles, historical vocabulary, project-descriptive labels and bounded unknowns.
- Reworked `precomp/guides/FORMAT_REFERENCE.md` and `precomp/guides/TOOLS_AND_METHOD.md` to connect PRECOMP to the broader VFX renderer without collapsing the layers together.

### Beginner documentation and naming explanation

- Added a detailed beginner explanation of why reverse engineering can prove behavior even when the original EA source name is no longer recoverable.
- Explained surviving RTTI/class names, runtime command strings, Ghidra-generated `FUN_...` names, compiler optimization, inlining and cross-subsystem interfaces.
- Reinforced the publication rule:

```text
real EA name found and correctly attached    -> use it
behavior/dataflow proven but EA name missing -> describe the behavior
neither proven                               -> keep it unknown
```

### Documentation organization and game-version source of truth

- Added `modding-resources/GAME_VERSION.md` as the single canonical source for:
  - The Sims 4 research version;
  - The Sims 4 patch date;
  - build-specific corpus anchors;
  - exact DX11/Win32 PRECOMP sizes and hashes;
  - recorded PRECOMP file timestamps;
  - compatibility/revalidation policy.
- Clarified that versioned template filenames and embedded fixture comments may retain their own tested snapshot while `GAME_VERSION.md` remains the canonical documentation-wide fixture declaration.

## 2026-08-22 — Label provenance audit

- Added the label-provenance policy distinguishing:
  - current byte/dataflow proof;
  - first-party historical EA/Maxis PRECOMP vocabulary;
  - TS4 VFX Tool historical/community SWARM vocabulary;
  - project structural/descriptive names;
  - intentionally unresolved fields.
- Clarified that the modern DX11 outer `Effect`/`Dx11EffectRecord` name is a project structural label, not an asserted stripped EA private type name.
- Kept `Technique` and `Pass` as maintained current navigation vocabulary because their current nesting/dataflow is independently proven and the terms also have first-party historical PRECOMP provenance.
- Clarified the status of `VSRef`, `PSRef`, `CSRef`, `StateStart`, `StateCount`, `ShaderPreparation*`, `StatePairBoundRaw` and current Win32 Version 2 SPAR words.
- Audited maintained labels against TS4 VFX Tool metadata/strings, the preserved first-party 2014 PRECOMP template and the validated installed PRECOMP fixtures.

## 2026-08-21 — Initial maintained PRECOMP research set

### DX11

- Published the maintained `DATA` / `shaders` payload Version 6 structure.
- Documented the established outer record -> Technique -> Pass graph.
- Documented 1-based VS / PS / CS references, with `0` meaning no shader for that stage.
- Proved source-record `+0x00` -> exact physical compact shader-stream mapping across the full documented corpus.
- Proved compact shader storage as Raw Snappy -> DXBC and validated the reconstructed shaders through Direct3D inspection/creation paths.
- Kept unresolved private fields as `Unknown`, `WordN`, raw records or descriptive physical roles rather than guessing them.

### Win32 Version 2

- Published a separate current Win32 model instead of treating Win32 as a variation of DX11.
- Mechanically validated the maintained prefix:

```text
SPKG -> SHDB -> LINK -> PlatformShader[] -> DBUF -> SPAR -> SMDB
```

- Deliberately did not import the historical Version 1 `TECH/PASS/PARM` tail without independent Version 2 validation.

### Renderer/VFX semantic cross-check

- Closed the bounded research matrix for the then-published renderer scope covering shared VFX renderer paths, Classic particle/sprite, instanced/non-instanced Model Particle, Dynamic and Decal routes.
- Classified the investigated `ep12_pom_high_skill_fail01_left` `drawMode 0x83` case as ordinary Model Particle/MLOD geometry rather than a distinct geometry renderer, without generalizing the broader meaning of authored `0x83`.

### Editor templates

- Published maintained 010 Editor `.bt` templates for DX11 and current Win32 Version 2.
- Added native ImHex `.hexpat` ports for the same validation boundaries.
- Runtime-tested all four maintained editor/fixture combinations.
- Corrected the 010 Editor Win32 `PlatformShader[]` handling to disable invalid fixed-size optimization for variable-sized records.

### Historical references

- Preserved the old TS4 Win32 Version 1 template published by SimGuruModSquad/Maxis as a separately attributed historical reference.
- Added the TS3 community template by Lyralei/Tashiketh as a separately attributed historical reference.
- Documented why historical names/layouts are not automatically current proof.
