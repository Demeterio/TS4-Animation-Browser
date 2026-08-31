# Tools and evidence method

The maintained PRECOMP/VFX documentation was built by checking the same pipeline from several directions: installed game resources, current CPU/runtime behavior, D3D11 state/draws and final shader bytecode.

No single third-party tool is treated as the source of truth. Current installed game bytes and current EA runtime behavior have priority.

For the exact build, fingerprints and build-specific corpus populations, use:

```text
../../GAME_VERSION.md
```

For the exact naming/confidence policy, read:

```text
LABEL_PROVENANCE.md
```

For the end-to-end result produced by this method, read:

```text
../../vfx/PACKAGE_TO_RENDER.md
```

## Evidence principle

The basic rule is:

```text
current bytes/runtime behavior
-> exact dataflow / physical GPU role
-> historical first-party vocabulary when independently transferable
-> historical/community correlation
-> project descriptive label
-> UNKNOWN / UNKNOWN_BOUNDED
```

A matching word in an old template is not enough. A plausible D3D convention is not enough. An effect that looks visually similar is not enough.

Conversely, the lack of an original private field name does not erase a mechanically exact offset/stride/dataflow relation.

## Main tools

### 010 Editor

Used for manual binary inspection and for the distributable `.bt` Binary Templates in this resource set.

010 Editor is useful for exploring structured bytes interactively. Corpus-wide proof such as validating a full shader table is better handled by dedicated tooling rather than manual navigation.

### ImHex

Used as the free/open-source editor target for the maintained `.hexpat` Pattern Language ports.

ImHex and 010 Editor use different languages. The ImHex patterns are native ports of the same validated boundaries, not direct execution/conversion of `.bt` files.

### Sims 4 Studio

Used as a Sims 4 modding/resource reference and for controlled content inspection/tests.

### Andrew's Studio Effect Player

Used as an in-game VFX playback helper so known effects could be triggered reproducibly for targeted GPU/runtime inspection.

It is a test/visualization helper, not a source of PRECOMP field semantics.

Reference:

https://sims4studio.com/post/43316

### Ghidra

Used for bounded static analysis of known loader, VFX and renderer targets.

Later research passes deliberately followed specific xrefs/dataflow questions instead of broad decompilation. This was particularly useful for closing current SWARM factories/runtime ownership, `.swb/.swb2/.swh2` resource selection, PRECOMP state consumers and renderer-selection paths.

### RenderDoc

Used for D3D11 frame captures and exact draw-level inspection:

```text
input layouts
vertex/index buffers
shader bindings
textures/resources
render state
draw parameters
```

This connects offline binary/resource interpretations to physical GPU behavior.

A RenderDoc snapshot is fixture evidence. It must not be promoted to a global renderer/state rule when the engine has a generic consumer that can be analyzed directly.

### 3Dmigoto

Used for targeted D3D11 shader/resource capture/inspection while connecting known visible VFX to concrete GPU state.

### Frida

Used for narrow runtime probes when a static-analysis result required object identity, writer/consumer or branch confirmation at runtime.

Later probes were intentionally bounded to avoid collecting unrelated renderer activity.

### WinDbg

Used for selected native runtime/debugging anchors where another independent check was useful.

### .NET 8 / C# tooling

Dedicated inspection/validation tools were used for:

- DBPF/resource inventory;
- SWARM/VFX decoding;
- PRECOMP structure checks;
- Raw Snappy reconstruction;
- DXBC validation/reflection/disassembly;
- shader-stage/source mapping;
- resource-topology audits;
- model/material/texture graph validation;
- reproducible semantic/evidence reports;
- validation that fixture-scoped evidence cannot silently become a global manifest rule.

### PowerShell and Python

Used for repeatable runners, corpus checks and focused binary/data analysis.

## Historical/community references

### TS4 VFX Tool by Denton47

TS4 VFX Tool is valuable historical/community reference material for authored VFX/SWARM data.

Reference:

https://modthesims.info/showthread.php?t=639339

Its reader/writer/model layer contains substantial historical structure for families such as MetaparticleEffect, SequenceEffect, SoundEffect, ShakeEffect, GameEffect and DistributeEffect.

That makes it useful for **layout/name candidates**, but not authority for current bytes by itself.

A historical field name is promoted only when current serialization/read order/dataflow supports it.

### Preserved EA/Maxis PRECOMP template

The historical TS4 Win32 Version 1 template under `old-references/` identifies itself as an Electronic Arts 2014 Binary Template.

It preserves first-party historical vocabulary such as:

```text
PlatformShader
Technique / TECH
Pass / PASS
ShaderParam / PARM
Sampler
render-state structures
```

This is stronger historical PRECOMP vocabulary than community inference, but it still describes its own old format generation.

A concrete example of the transfer rule is Win32 Version 2 SPAR: historical Version 1 provides richer member names, while the maintained current Version 2 parser keeps the words raw because those semantics were not independently revalidated for Version 2.

## DX11 PRECOMP validation sequence

The core modern PRECOMP proof followed this order:

```text
1. Locate/bound DATA and the "shaders" payload.
2. Establish current version, counts, record sizes and relative-pointer behavior.
3. Prove outer effect-like record -> Technique -> Pass navigation.
4. Prove 1-based VS/PS/CS references and 0 = absent.
5. Recover the physical compact shader corpus.
6. Prove source-record +0x00 -> exact physical compact stream.
7. Reconstruct Raw Snappy streams.
8. Validate reconstructed payloads as DXBC.
9. Reflect/disassemble and create shaders through D3D11.
10. Cross-check representative VFX draws against real GPU bindings/state.
11. Leave unrelated/private fields unknown unless separately proven.
```

Exact stage populations and corpus-wide reconstruction counts belong in `../../GAME_VERSION.md` rather than this methodology guide.

The outer `Effect` label is deliberately structural/project terminology. The pointers/counts/dataflow proof does not require claiming a private stripped EA class name.

## Generic PRECOMP state-consumer validation

State semantics were not closed by matching plausible raw values to RenderDoc output.

The final generic-state method followed the common current consumer:

```text
1. Start from Pass StateStart / StateCount.
2. Follow the serialized {stateId, rawValue} pair into the common cached setter.
3. Follow the dispatcher branch for the exact state ID.
4. Identify the destination D3D11 descriptor field or bind parameter.
5. Follow helper translators instead of assuming raw numeric values already equal public D3D enums.
6. Recover exact float/packed-value transforms where present.
7. Keep the private source-level name bounded when it does not survive.
```

This produced mechanically exact public destinations for IDs `0x00..0x1E` without relying on a named VFX fixture.

Important examples of direct helper/mechanics work include:

```text
comparison translator
stencil-op translator
blend-factor translator
blend-op translator
0x13 raw-bit float reinterpretation
0x14 context-dependent depth-bias scaling
0x1A packed-byte -> float factor
0x1B blend-descriptor transformation
```

State `0x1B` demonstrates the stopping rule particularly well:

```text
consumer transform   exact
D3D effect           exact
private source name  unavailable
```

Correct publication: exact mechanics + `UNKNOWN_BOUNDED` private purpose.

## VFX/SWARM validation sequence

The later VFX work separated the following questions instead of trying to infer everything from the final pixels:

```text
1. What authored family record is serialized?
2. How is its current body/version parsed?
3. What Description/Effect/runtime object is created?
4. Does it create/update child effects, a non-GPU endpoint or renderer-facing data?
5. If graphics are required, which renderer family is actually reached?
6. What model/geometry/material/texture resources are resolved?
7. What PRECOMP selection is actually made when applicable?
8. What D3D11 input/state/shaders are physically consumed?
```

This produced the maintained rule:

```text
authored SWARM family != CPU simulation path != GPU renderer family
```

The canonical modder-facing family inventory is:

```text
../../vfx/AUTHORED_FAMILIES.md
```

The representative renderer reference is:

```text
../../vfx/RENDERER_REFERENCE.md
```

## Merged/split resource-topology method

The current `.swb/.swb2/.swh2` question was not answered by comparing names.

The resource-side audit joined serialized identities:

```text
VisualEffectsMerged raw handle -> Effect IID
Effect IID                     -> VisualEffectsInstanceMap MasterIID
MasterIID                      -> effective split VisualEffects resource
```

The current executable route was then checked separately. Exact current strings/resource lookup dataflow established:

```text
Sims4EffectsOpt  .swb2
Sims4EffectsOptH .swh2
Sims4Effects     .swb
SwarmDisableCollectionStreaming
```

Build-specific identity populations are centralized in `../../GAME_VERSION.md`.

This distinction matters: **resource identity coverage** and **runtime route availability** are different questions.

See:

```text
../../vfx/RESOURCE_TOPOLOGY.md
```

## Representative renderer cross-checks

The renderer work used controls across multiple physical paths rather than one visually convenient effect:

- Classic dynamic particle/sprite geometry;
- Dynamic `013f`;
- Model Particle non-instanced;
- Model Particle instanced;
- Decal;
- the investigated EP12 authored `drawMode 0x83` case.

Evidence combined current VFX data, controlled playback, CPU/runtime dataflow and RenderDoc/3Dmigoto state.

This is why an authored Ribbon can be proven to reach Dynamic `013f` for one fixture without turning “RibbonEffect = Dynamic 013f” into a universal rule.

## Model/geometry/material validation

For the bounded Model Particle route, current MLOD source records establish a direct relation to:

```text
materialReference
VRTF
VBUF
IBUF
```

The renderer work then checks how those resources become real D3D11 vertex/index data.

The instanced route was additionally tied to an exact per-instance vertex stream and `DrawIndexedInstanced`.

The material graph is validated as a separate resource layer:

```text
mesh materialReference
-> direct MATD
or
-> MTST -> serialized MATD leaves
-> ShaderData/resource keys/textures as applicable
```

All serialized MTST entries are retained in the normalized graph. No convenient variant is silently selected just to produce a renderable result.

Explicit missing/unresolved resource classes are retained rather than replaced with arbitrary geometry, materials or textures. Current build-specific counts are in `../../GAME_VERSION.md`.

## Runtime PRECOMP attribution method

Structural PRECOMP decoding does not identify which record a runtime effect selects.

A route-specific attribution must follow something equivalent to:

```text
runtime producer/context
-> selector/input values
-> selected PRECOMP record/technique
-> pass when resolved
-> shader refs/state slice
-> exact renderer callback or submission scope
```

Candidate ranking, hash similarity or a visually plausible shader pair is not sufficient.

The investigated Decal route and the maintained instanced Model Particle fixture are examples where exact runtime/PRECOMP attribution was established at the appropriate scope.

## Fixture regression versus generic semantics

Fixtures answer questions such as:

```text
what IA layout/state was actually bound for this draw?
does our implementation reproduce the proven route?
```

They do not establish universal rules for authored families, renderer families or PRECOMP state IDs.

The normalized manifest deliberately isolates fixture pipeline states from global routes to prevent this accidental promotion.

## Why template/editor success is not the same as format proof

A `.bt` or `.hexpat` successfully running is useful evidence that the editor representation matches the tested file.

It does not, by itself, prove:

- every private field name;
- that a later game build is identical;
- that a historical field mapping still applies;
- that compressed shader bytes reconstruct correctly;
- that a renderer consumes a field the way a label suggests.

That is why editor-template tests are combined with parser/corpus/runtime/GPU evidence.

## Negative evidence and stop rules

A good audit records failed hypotheses too.

Examples from the maintained research:

- the historical Decal 16/16 event is real, but targeted work did not establish ownership by the closed 44/4 route;
- Classic PRECOMP candidates can remain non-unique;
- an exact Model Particle geometry match does not identify a unique authored owner;
- strong MATD/PRECOMP raw-key equality does not by itself prove direct selector causality.

When a private semantic is not required for mechanics and no first-party name survives, stop with `UNKNOWN_BOUNDED`.

## Unknown-field policy

A binary template becomes dangerous when a convenient label looks more certain than the evidence behind it.

Therefore:

- exact offsets/sizes can coexist with unknown private names;
- exact GPU roles can coexist with unknown original C++ member names;
- old first-party names can remain historical until current transfer is proven;
- community tools can accelerate discovery without becoming authority;
- correlations remain labeled correlations;
- unsupported branches/failures are reported explicitly rather than silently coerced into a known path.

## When to change the editor templates

Change `.bt` / `.hexpat` when evidence changes the **serialized model**, for example:

```text
record size
field offset
relative-pointer rule
table count/boundary
serialized field role needed for safe navigation
```

Do not rewrite the parser body merely because a downstream consumer is better understood.

The generic state-consumer work did not change the 8-byte state-pair representation or the Pass/state-table layout, so no template-body change is required for that semantic improvement alone.

## Revalidation after a game update

At minimum:

1. fingerprint EXE/DX11/Win32 PRECOMP;
2. re-run structural counts/bounds;
3. revalidate stage-reference/source-stream mapping if PRECOMP changed;
4. re-check state dispatcher/translators if the executable changed;
5. re-run route-specific fixture checks only where affected;
6. preserve previously bounded semantics unless new evidence genuinely promotes them.

## Publication checklist

Before publishing a new PRECOMP/VFX claim:

```text
[ ] exact build/fingerprint stated or inherited from canonical fixture
[ ] serialized offsets/counts bounded where applicable
[ ] current consumer identified when semantics depend on runtime
[ ] fixture claim scoped as fixture/route, not global
[ ] historical names labelled historical unless independently current
[ ] private name not invented
[ ] negative/ambiguous outcome retained when relevant
```
