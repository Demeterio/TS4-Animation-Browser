# The Sims 4 VFX / SWARM modding reference

Fixture: see `../GAME_VERSION.md`.

This directory is the maintained **public modder-facing reference** for the VFX/SWARM pipeline studied by TS4 Animation Browser.

It focuses on information that is useful outside the application: resource types, authored families, resource resolution, renderer behavior, PRECOMP linkage, tested fixtures, and the boundary between proven behavior and still-unknown private semantics.

For the exact game build of The Sims 4, patch date, build-specific corpus anchors, closure metrics and binary fingerprints used by these documents, see:

```text
../GAME_VERSION.md
```

## Recommended reading order

```text
README.md
RESOURCE_TOPOLOGY.md
AUTHORED_FAMILIES.md
TESTED_EFFECT_FIXTURES.md
PACKAGE_TO_RENDER.md
RENDERER_REFERENCE.md
```

- `RESOURCE_TOPOLOGY.md` explains `.swb`, `.swb2`, `.swh2`, `Effect IID -> MasterIID` and the validated split/streaming and merged routes.
- `AUTHORED_FAMILIES.md` lists the active authored families in the validated modern corpus and explains which are renderer-facing, orchestration-oriented or non-GPU.
- `TESTED_EFFECT_FIXTURES.md` lists the named VFX controls, captures, runtime observations, identity examples and fixture-specific PRECOMP ownership boundaries retained as research anchors.
- `PACKAGE_TO_RENDER.md` follows the complete bounded path from a DBPF package to a D3D11 draw, including the current common runtime PRECOMP context/selector resolver.
- `RENDERER_REFERENCE.md` collects renderer families and physical GPU/resource facts that are safe to reuse within their stated scope.

For shader-package structure and editor templates, use:

```text
../precomp/README.md
../precomp/guides/FORMAT_REFERENCE.md
../precomp/guides/LABEL_PROVENANCE.md
```

For a beginner explanation of the whole stack, including why some real EA names survive compilation while others do not:

```text
../BEGINNER_GUIDE.md
```

## Validated corpus

The exact build, patch date, populations and percentages are intentionally **not duplicated here**. See `../GAME_VERSION.md`.

For that documented fixture, the modern VFX corpus is mechanically closed with all active authored-family transitions classified as either semantically known or explicitly bounded/unknown. Those are research/evidence claims for the documented fixture; they are not a measure of how much of any viewer or third-party tool has been implemented.

## Core mental model

A useful simplified path is:

```text
DBPF package
  -> effective VFX resource
  -> VisualEffect definition
  -> authored SWARM family record(s)
  -> runtime Description / Effect / child orchestration
  -> CPU simulation or non-GPU side effect
  -> renderer family when graphics are required
  -> model/geometry/material/texture resolution when applicable
  -> runtime PRECOMP context/selector selection when applicable
  -> PRECOMP Effect-like record -> Technique -> Pass
  -> VS / PS / CS + render-state slice
  -> D3D11 resource/state binding
  -> Draw / DrawIndexed / DrawIndexedInstanced / compute when applicable
```

Do not turn that into a one-to-one mapping. A single authored effect can combine several family records, spawn children, play sound and render geometry. Different authored families can converge on the same renderer path, and the same authored family can reach more than one renderer path depending on data.

## Authored family is not renderer family

This is the most important rule in these documents:

```text
authored SWARM family
        !=
runtime simulation/orchestration class
        !=
PRECOMP selection
        !=
GPU renderer family
```

Examples:

- `ParticleEffect` can reach Classic, Model Particle or Dynamic renderer behavior depending on data.
- at least one `RibbonEffect` reaches the observed Dynamic `013f` renderer, but that is not generalized to every Ribbon.
- an investigated Particle fixture also reaches Dynamic `013f`; this demonstrates convergence without turning either authored family into a renderer alias.
- the investigated `DecalEffect` route has an end-to-end mechanically proven runtime -> PRECOMP -> 44/4 indexed `DrawIndexed` path; the 44/4 layout is evidence for that route, not a universal serialized property of family `0x03`.
- a separate historical `16/16` RenderDoc event was captured while the Decal fixture was active, but exact ownership by the mechanically closed Decal route is not established and no 44/4-to-16/16 bridge should be modeled.
- `SoundEffect` uses an audio/control endpoint rather than requiring geometry.
- `SequenceEffect`, `VisualEffect`, `MetaparticleEffect` and `DistributeEffect` can orchestrate child effects.
- `Model Particle` is a renderer behavior using model/MODL/MLOD geometry; authored `ModelEffect` is a separate concept.

## Model/material resource relationship

The current normalized resource graph proves a durable relationship that should be modeled independently from named fixtures:

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

All serialized MTST entries are retained by the maintained oracle. A consumer must not silently choose an arbitrary material variant simply because it renders something plausible.

Current populations and missing-resource counts are kept in `../GAME_VERSION.md`.

## Runtime PRECOMP resolver relationship

The current executable provides a common mechanical context/selector bridge that is distinct from exact named-effect PRECOMP ownership.

For the common transient resource-backed path:

```text
runtime resource/material-side object +0x4C 32-bit key
-> lookup / variant-context resolution
-> transient context candidates +0x18 / +0x20
-> transient selector +0x28
-> selected context + selector
-> common PRECOMP dispatch
```

SceneModel independently resolves the same high-level pair from an explicit record context or owner context slots, plus a record selector or owner fallback.

This proves how current runtime data reaches the PRECOMP dispatcher. It does **not** prove either of these stronger statements:

```text
serialized MATD Shader field == runtime object+0x4C
runtime object+0x4C == PRECOMP outer effect-like +0x00 universally
```

Likewise, exact common resolver mechanics do not turn shader-compatible PRECOMP candidates into unique ownership for a named effect. `PACKAGE_TO_RENDER.md` and `../precomp/guides/FORMAT_REFERENCE.md` document the detailed generic boundary.

### Final targeted named-ownership result

The maintained repeated trigger/control campaign for:

```text
clue_sparkle
ability_transform_sparkle_trail
gp07_mother_plant_spray
```

did not produce an exclusive named-effect -> PRECOMP record/selector mapping.

`clue_sparkle` repeatedly observed a Classic-compatible `record 53275 / selector 1`, but the same pair remained active in control windows and follows the shared transient Particle-context route. The two Dynamic `013f` controls produced no selections from their configured exact shader-compatible candidate set during the targeted trigger windows.

These are ownership/context boundaries, not failures of the proven Classic/Dynamic renderer or DX11 shader-table mappings.

Current classification for unique named PRECOMP ownership of all three controls is:

```text
UNKNOWN_BOUNDED
```

The targeted campaign is closed for the documented build. Fixture-specific details and non-claims are retained directly in `TESTED_EFFECT_FIXTURES.md`.

## PRECOMP render-state relationship

Pass state is also kept separate from fixture observations.

The generic current consumer is:

```text
PRECOMP Pass
-> StateStart / StateCount
-> {stateId, rawValue}
-> common EA state dispatcher
-> D3D11 depth/stencil, rasterizer and blend state
```

The supported state-ID mapping comes from current consumer dataflow, not from inferring a global rule from six RenderDoc snapshots. Fixture pipeline states remain regression evidence for those exact captured draws.

Detailed state mechanics are documented in `../precomp/guides/FORMAT_REFERENCE.md`; build-specific closure metrics remain in `../GAME_VERSION.md`.

## What “proven” means here

These public documents distinguish evidence classes such as:

```text
EA_NAME_PROVEN
DATAFLOW_PROVEN
PHYSICAL_SEMANTIC_PROVEN
CORRELATED
UNKNOWN_BOUNDED
CONTEXT_ONLY
```

An exact offset can be proven while its original private member name is unknown. A renderer role can be proven without inventing an EA class name. A historical/community name is not promoted to current fact until current bytes/dataflow support it.

A fixture-level runtime observation is not automatically a corpus-wide rule.

## Compatibility boundary

The resource counts and exact build-specific observations in this set are tied to the fixture in `../GAME_VERSION.md`. A later patch may preserve the same format, add records or change data.

Treat a changed resource/version/hash as a reason to revalidate the affected boundary, not as automatic proof that everything changed and not as permission to weaken parser checks.
