# TS4 Animation Browser — Modding Resources & Technical Documentation

This directory contains **public modding resources and technical documentation** published alongside TS4 Animation Browser.

The material is intended for modders, animators, technical artists, researchers and tool developers who want to understand the game data and rendering paths studied while developing the application. It is not required to run TS4 Animation Browser.

Public project:

https://github.com/Demeterio/TS4-Animation-Browser

Additional Sims 4 tutorials and resources by Demeterio:

https://demeterio.tumblr.com/tutorials-and-resources-for-modders-in-sims-4

## Found an error, approximation or conflicting result?

These resources document behavior that has been validated against specific The Sims 4 builds, files and research fixtures. Even with conservative evidence rules, documentation can still contain mistakes, unclear wording, incomplete conclusions, outdated information after a game update, or a result that does not reproduce on another installation.

If you find something that appears incorrect, approximate, ambiguous or inconsistent with your own test, please open an issue on the public TS4 Animation Browser GitHub repository and select the issue category/template that best matches what you found:

https://github.com/Demeterio/TS4-Animation-Browser/issues/new/choose

When possible, include the relevant documentation page, your The Sims 4 game version, the resource/effect/file involved, what you expected, what you observed, and any reproducible evidence that may help verify the difference.

Reports that challenge an existing conclusion are welcome. The goal of these resources is to stay as close as possible to the actual game data and runtime behavior, and to correct or narrow a claim when better evidence becomes available.

## Start here

```text
modding-resources/
├─ _HOW_TO_OPEN_MARKDOWN_ON_WINDOWS.txt
├─ README.md
├─ BEGINNER_GUIDE.md
├─ GAME_VERSION.md
├─ CHANGELOG.md
├─ archives/
├─ precomp/
└─ vfx/
```

Recommended entry points:

- `BEGINNER_GUIDE.md` — beginner introduction to VFX, SWARM, PRECOMP, renderer concepts, binary templates and EA-name provenance;
- `GAME_VERSION.md` — **canonical The Sims 4 research build, patch date, corpus anchors and PRECOMP fingerprints**;
- `CHANGELOG.md` — project-wide change history for the public modding resources;
- `archives/README.md` — dated ZIP snapshots, archive naming and publication history;
- `precomp/README.md` — shader PRECOMP formats, `.bt`/`.hexpat` templates and validation boundaries;
- `vfx/PACKAGE_TO_RENDER.md` — end-to-end VFX package -> runtime -> renderer -> PRECOMP -> D3D11 path;
- `vfx/AUTHORED_FAMILIES.md` — validated authored VFX/SWARM family inventory and roles;
- `vfx/TESTED_EFFECT_FIXTURES.md` — named VFX controls, captures and identity examples used as research anchors;
- `precomp/guides/LABEL_PROVENANCE.md` — exact provenance/confidence rules for technical names.

## Current files and dated snapshots

The files directly under `modding-resources/` are the latest maintained version of the public resources.

The `archives/` directory contains intentionally published, dated ZIP snapshots for reproducibility and convenient download. Snapshot dates describe the publication date of the modding resources; they do not replace the exact The Sims 4 fixture recorded in `GAME_VERSION.md`.

Each published archive is self-contained and excludes older archives. See `archives/README.md` for the archive naming, tags and publication history.

## One source of truth for the game version

Do **not** copy the research game version, patch date or PRECOMP hashes into multiple documents.

The canonical fixture reference is:

```text
GAME_VERSION.md
```

Other documents may describe counts/layouts for the validated corpus, but should link to `GAME_VERSION.md` for the exact game build of The Sims 4, its patch date and the fixture fingerprints.

Versioned template filenames and embedded fixture comments can retain the build they were authored/tested against; `GAME_VERSION.md` remains the canonical declaration for the documentation set. The exact rule is documented there.

## The most important distinction

The full VFX path contains several different layers:

```text
DBPF package/resource
        |
        v
authored VFX/SWARM data
        |
        v
runtime effect/simulation/orchestration
        |
        v
renderer family or non-GPU endpoint
        |
        v
model/material/texture/shader resource resolution
        |
        v
PRECOMP Effect-like record -> Technique -> Pass
        |
        v
VS / PS / CS
        |
        v
D3D11 bindings and draw/dispatch
```

Those layers must not be collapsed into one concept.

In particular:

```text
authored SWARM family != CPU simulation path != GPU renderer family != shader
```

A `SoundEffect` can be part of a VFX without creating geometry. A `SequenceEffect` can orchestrate children. A `RibbonEffect` is not automatically one specific GPU renderer. `Model Particle` is an observed renderer behavior and is **not** the same thing as the authored `ModelEffect` family.

## Validated VFX resource topology

The executable from the validated build recognizes three related VFX resource types:

```text
0x1B192049  VisualEffects
0x1B19204A  VisualEffectsInstanceMap
0xEA5118B0  VisualEffectsMerged
```

The validated EA resource-name/extension mapping is:

```text
VisualEffects            -> Sims4EffectsOpt  -> .swb2
VisualEffectsInstanceMap -> Sims4EffectsOptH -> .swh2
VisualEffectsMerged      -> Sims4Effects     -> .swb
```

The validated merged library is completely mapped through `Effect IID -> MasterIID` to existing split `VisualEffects` resources for the corpus identified in `GAME_VERSION.md`. The executable from that build also retains the merged `.swb` load route, selected by the `SwarmDisableCollectionStreaming` branch.

See:

```text
vfx/RESOURCE_TOPOLOGY.md
```

## PRECOMP in one paragraph

`Shaders_DX11.precomp` is the modern Direct3D 11 shader package studied here. The validated route is:

```text
DATA / shaders payload v6
-> outer effect-like record
-> Technique
-> Pass
-> 1-based VS / PS / CS reference
-> stage source record
-> physical Raw Snappy stream
-> DXBC
-> D3D11 shader
```

The `.precomp` file, template and editor are different things:

```text
EA .precomp file = installed game data used by the game
.bt file         = 010 Editor Binary Template describing validated structure
.hexpat file     = ImHex Pattern Language equivalent
010 Editor/ImHex = external inspection tools
```

Exact shader counts and PRECOMP fingerprints belong in `GAME_VERSION.md`.

## Evidence and naming policy

The documentation follows a conservative evidence hierarchy:

- **EA_NAME_PROVEN** — exact current first-party identity attached to the relevant object/resource/field;
- **DATAFLOW_PROVEN** — producer/storage/consumer relationship establishes the role;
- **PHYSICAL_SEMANTIC_PROVEN** — GPU input, format, stride, shader consumption or another physical behavior establishes the role;
- **historical first-party** — useful EA/Maxis vocabulary from an older representation, not automatically current;
- **historical/community** — useful comparison material, never current proof by itself;
- **project structural/descriptive** — useful name for a proven structure whose stripped EA private name is not known;
- **UNKNOWN_BOUNDED** — mechanics are constrained but the exact private name/finer meaning is not proven.

The beginner-friendly explanation of *why behavior can be proven while original EA names are missing* is in `BEGINNER_GUIDE.md`.

## Historical and third-party material

Historical templates and community references are kept separately from maintained current material. Their original attribution is preserved wherever known.

They are valuable for vocabulary and comparison, but an old field name does not prove that a newer game format retained the same layout or semantics.

## What is not redistributed here

The maintained resource set does not redistribute installed EA PRECOMP files, extracted shader bytecode, executable code, textures, meshes, animations or VFX game assets.

Use files from your own legitimately installed copy of The Sims 4 when following the inspection guides.

## Copyright and disclaimer

Copyright © 2026 Demeterio for original Demeterio-authored documentation, maintained templates and project research material in this directory, except where a file is explicitly identified as historical or third-party material with different authorship or rights.

The Sims, The Sims 3, The Sims 4, Maxis, Electronic Arts and related names, trademarks, software and game assets are the property of Electronic Arts Inc. and/or their respective rights holders.

TS4 Animation Browser and these resources are independent fan-made projects. They are not affiliated with, endorsed by, sponsored by or officially supported by Electronic Arts or Maxis.
