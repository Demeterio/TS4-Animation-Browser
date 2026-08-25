# The Sims 4 VFX / SWARM modding reference

Fixture: see `../GAME_VERSION.md`.

This directory is the maintained modder-facing reference for the VFX/SWARM pipeline studied by TS4 Animation Browser.

It focuses on **what is useful outside the application**: resource types, authored families, resource resolution, renderer families, PRECOMP linkage and the boundary between proven behavior and still-unknown private semantics.

For the exact game build of The Sims 4, patch date and build-specific corpus anchors used by these documents, always see:

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
- `TESTED_EFFECT_FIXTURES.md` lists the named VFX controls, captures, runtime observations and identity examples retained as research anchors.
- `PACKAGE_TO_RENDER.md` follows the complete bounded path from a DBPF package to a D3D11 draw.
- `RENDERER_REFERENCE.md` collects renderer families and physical GPU layouts that are safe to reuse within their stated scope.

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

The exact build and patch date are intentionally **not duplicated here**. See `../GAME_VERSION.md`.

For that documented fixture, the modern VFX corpus is mechanically closed with all active authored-family records classified as either semantically known or explicitly bounded/unknown. The percentages used by the internal research planning model describe the evidence model, not how much of a third-party viewer has been implemented.

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
  -> referenced geometry/material/texture resources
  -> PRECOMP Effect-like record -> Technique -> Pass
  -> VS / PS / CS
  -> D3D11 resource binding
  -> Draw / DrawIndexed / DrawIndexedInstanced / compute when applicable
```

Do not turn that into a one-to-one mapping. A single authored effect can combine several family records, spawn children, play sound and render geometry. Different authored families can converge on the same renderer path.

## Authored family is not renderer family

This is the most important rule in these documents:

```text
authored SWARM family
        !=
runtime simulation/orchestration class
        !=
GPU renderer family
```

Examples:

- `ParticleEffect` can reach Classic, Model Particle or Dynamic renderer behavior depending on data.
- at least one `RibbonEffect` reaches the observed Dynamic `013f` renderer, but that is not generalized to every Ribbon.
- `SoundEffect` uses an audio/control endpoint rather than requiring geometry.
- `SequenceEffect`, `VisualEffect`, `MetaparticleEffect` and `DistributeEffect` can orchestrate child effects.
- `Model Particle` is a renderer behavior using model/MODL/MLOD geometry; authored `ModelEffect` has zero references in the documented modern corpus and is a separate concept.

## What “proven” means here

These public documents intentionally distinguish:

```text
EA_NAME_PROVEN
DATAFLOW_PROVEN
PHYSICAL_SEMANTIC_PROVEN
CORRELATED
UNKNOWN_BOUNDED
CONTEXT_ONLY
```

An exact offset can be proven while its original private member name is unknown. A renderer role can be proven without inventing an EA class name. A historical/community name is not promoted to current fact until current bytes/dataflow support it.

## Compatibility boundary

The resource counts and exact structures in this set are tied to the fixture in `../GAME_VERSION.md`. A later patch may preserve the same format, add records or change data.

Treat a changed resource/version/hash as a reason to revalidate the affected boundary, not as automatic proof that everything changed and not as permission to weaken parser checks.
