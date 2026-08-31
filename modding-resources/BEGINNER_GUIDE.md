# Beginner guide: VFX, shaders, PRECOMP and binary templates

This guide is for The Sims 4 modders who are curious about VFX/shader resources but do not already work with binary formats, native executables or Direct3D.

For the exact game build of The Sims 4, patch date, corpus anchors and binary fingerprints used by the maintained documentation, always see:

```text
GAME_VERSION.md
```

Build-specific facts below refer to that **validated/documented fixture**. They do not automatically describe the latest The Sims 4 patch available when you read this guide.

## The five things to keep separate

Most confusion disappears once these are treated as different layers:

```text
1. .package / DBPF resources = game content and references
2. VFX / SWARM               = authored effect data and behavior
3. renderer                  = CPU/GPU path that prepares visible graphics
4. .precomp                  = EA's packaged compiled shader data/lookup structures
5. .bt / .hexpat             = text templates for inspecting .precomp files
```

The Sims 4 uses the `.package` and `.precomp` data. It does **not** use the `.bt` or `.hexpat` files in this repository.

A useful analogy for PRECOMP is:

```text
.precomp         = the building
.bt/.hexpat      = a floor plan
010 Editor/ImHex = the program that overlays the plan on the building
```

## What is a VFX?

VFX means **visual effect**, but an effect in The Sims 4 can contain more than pixels or particles.

A single authored effect can combine things such as:

- particles;
- model-based particles;
- trails/ribbons;
- decals;
- child effects;
- sequences/timing;
- sound;
- shake behavior;
- gameplay/context actions.

That is why “I played the effect and this family did not draw anything” is not enough to say the family is broken. Some families are orchestration or non-GPU behavior by design.

## What is SWARM?

In these documents, **SWARM** is the name used for The Sims 4 VFX data/runtime family studied in the validated modern `VisualEffects` resources.

Do not invent a long-form expansion for `SWARM`; the documentation uses the observed format/runtime terminology.

The validated modern authored data contains typed family records such as:

```text
ParticleEffect
MetaparticleEffect
DecalEffect
SequenceEffect
SoundEffect
RibbonEffect
SpriteEffect
...
```

The validated authored-family inventory and each family's bounded role are documented in:

```text
vfx/AUTHORED_FAMILIES.md
```

The named effects deliberately used as research controls, runtime observations and identity examples are collected in:

```text
vfx/TESTED_EFFECT_FIXTURES.md
```

## Authored family is not renderer family

This is the single most important VFX rule:

```text
authored SWARM family
        !=
CPU simulation/orchestration path
        !=
GPU renderer family
        !=
shader
```

Examples:

- `ParticleEffect` can reach different renderer behaviors depending on its data;
- one investigated `RibbonEffect` reaches Dynamic `013f`, but not every Ribbon is declared to do that;
- `SoundEffect` uses an audio/control endpoint rather than geometry;
- `SequenceEffect` can create/control child effects;
- `Model Particle` is a renderer behavior using MODL/MLOD geometry and is **not** the same thing as the authored `ModelEffect` family.

## Where VFX resources live

The Sims 4 stores resources in DBPF `.package` files.

DBPF packages contain many kinds of resources. A resource is identified by its Type, Group and Instance values, often shortened to **TGI**.

The validated VFX library uses three related resource types:

```text
0x1B192049  VisualEffects
0x1B19204A  VisualEffectsInstanceMap
0xEA5118B0  VisualEffectsMerged
```

The executable from the validated build associates them with:

```text
VisualEffects            -> Sims4EffectsOpt  -> .swb2
VisualEffectsInstanceMap -> Sims4EffectsOptH -> .swh2
VisualEffectsMerged      -> Sims4Effects     -> .swb
```

The `.swh2` map provides the exact identity route:

```text
Effect IID -> MasterIID -> split VisualEffects resource
```

The merged `.swb` library is still recognized by the executable from the validated build, while the validated split collection covers all merged identities found in the documented research fixture.

Full explanation:

```text
vfx/RESOURCE_TOPOLOGY.md
```

## What is a shader?

A shader is a small program executed by the GPU.

For a simplified D3D11 draw:

```text
vertex data
   |
   v
Vertex Shader (VS)
   |
   v
interpolated data
   |
   v
Pixel Shader (PS)
   |
   v
render target/output
```

The validated DX11 PRECOMP corpus also contains a small number of Compute Shaders (CS), which run general GPU work outside the normal vertex-to-pixel draw path.

The exact corpus counts belong to the fixture recorded in `GAME_VERSION.md`.

A shader is only one piece of rendering. The final draw can also need geometry, input layouts, textures, samplers, constant buffers, material/state data, blend/depth/culling state and transforms.

## Common renderer terms

| Term | Beginner meaning |
| --- | --- |
| CPU | processor code that loads resources, runs simulation and prepares renderer work |
| GPU | graphics processor that executes shaders and rendering work |
| vertex | one geometry input record, usually carrying position and other attributes |
| vertex buffer | GPU buffer containing vertex records |
| index | number selecting a vertex; lets triangles reuse vertices |
| index buffer | GPU buffer containing indices |
| UV / TEXCOORD | texture coordinates |
| texture | image/resource sampled by a shader |
| sampler | rules for filtering/addressing a sampled texture |
| constant buffer | GPU-readable parameters such as transforms/colors/other constants |
| input layout | mapping between bytes in vertex streams and shader inputs |
| draw call | command that submits geometry with the currently bound resources/state |
| semantic | shader input name such as `POSITION`, `COLOR`, `TEXCOORD` |
| IA | input assembler; the D3D11 stage that receives vertex/index input before the vertex shader |

A D3D semantic describes the shader interface. It does not automatically reveal the original EA business name of the CPU field that produced it.

## What is PRECOMP?

In these documents, **PRECOMP** refers to EA's packaged compiled/precompiled shader data and related lookup/pass structures. The documentation does not claim that `PRECOMP` is a recovered official long-form EA expansion.

The game ships compiled shader programs and related lookup/pass data rather than compiling every shader permutation from source during ordinary gameplay.

The DX11 file studied for the documented fixture is:

```text
The Sims 4\Game\Bin\res\Shaders_DX11.precomp
```

The validated hierarchy is:

```text
DATA
  -> root entry "shaders"
    -> payload Version 6
      -> outer effect-like record
        -> Technique
          -> Pass
            -> VS / PS / CS source reference
```

A Pass chooses shader stages using 1-based references:

```text
0 = stage absent
1 = first record in that stage table
2 = second record
...
```

The outer `Effect` / `Dx11EffectRecord` wording used in these documents is a structural project label. The exact private EA C++ name of that stripped outer type is not claimed.

## Does one VFX equal one PRECOMP record?

No. A named VFX and a PRECOMP outer record are different kinds of identity.

The safe model is:

```text
named VFX / authored graph
-> runtime branches and renderer routes
-> PRECOMP context + selector selection when applicable
-> PRECOMP outer record / Technique / Pass
```

Depending on the effect and runtime route, a named VFX can:

```text
reach no PRECOMP selection
reach one bounded PRECOMP selection
reach several PRECOMP selections across branches/routes
share a PRECOMP context or record with other effects
```

For example, one authored VFX can contain a visual child that reaches PRECOMP, another visual branch that selects a different route, and a `SoundEffect` branch that reaches no GPU shader at all.

So:

```text
PRECOMP-compatible shader/pass
!= exclusive ownership by a named effect
```

Structural PRECOMP decoding tells us what records, techniques and passes exist. Exact named-effect attribution requires separate runtime/dataflow evidence.

## Snappy, Raw Snappy and DXBC

These are different layers:

```text
PRECOMP stage source record
        |
        v
physical compact stream
        |
        v
Raw Snappy decompression
        |
        v
DXBC
        |
        v
D3D11 shader creation/use
```

**Snappy** is a lossless compression algorithm. It tells us how to reconstruct bytes; it does not tell us what those bytes mean.

In the validated DX11 PRECOMP, the compact shader streams are Raw Snappy data and the decompressed payload is a standard **DXBC** compiled shader container.

The exact shader population and file fingerprints are centralized in `GAME_VERSION.md`.

## What are PRECOMP render states?

A shader does not define every part of a D3D11 draw. A Pass also selects a slice of serialized render-state pairs:

```text
Pass
-> StateStart / StateCount
-> {stateId, rawValue}
-> common EA state consumer
-> D3D11 depth/stencil, rasterizer and blend state
```

The common consumer recovered from the validated executable mechanically explains state IDs `0x00..0x1E`. This mapping comes from validated runtime dataflow into D3D11 state structures, not from guessing what a few RenderDoc values appear to mean.

Examples of proven destinations include:

```text
depth enable / write / comparison
stencil enable / masks / operations
rasterizer fill / cull / scissor / depth bias
render-target blend factors / operations / write mask
```

One important naming boundary remains: state `0x1B` has an exact descriptor transformation, but its original private conceptual EA name has not been recovered. The documentation therefore keeps a structural name rather than inventing a friendly one.

Detailed state-ID/value mechanics are in:

```text
precomp/guides/FORMAT_REFERENCE.md
```

## What is Direct3D 11?

Direct3D is the graphics part of Microsoft's DirectX APIs. **D3D11** is the Direct3D generation used by the renderer path studied here.

D3D11 provides APIs for resources/state such as:

```text
vertex buffers
index buffers
input layouts
textures / shader resources
samplers
constant buffers
vertex/pixel/compute shaders
blend/depth/raster state
Draw / DrawIndexed / DrawIndexedInstanced
```

DXBC is shader data. D3D11 is the API that creates, binds and uses it.

## From a package to the screen

A simplified real VFX path is:

```text
DBPF package
  -> effective VisualEffects resource
  -> requested VisualEffect definition
  -> authored family records
  -> runtime Description / Effect / children
  -> simulation or side-effect endpoint
  -> renderer family if graphics are required
  -> model/geometry/material/texture resolution
  -> runtime PRECOMP context + selector selection when applicable
  -> PRECOMP outer record -> Technique -> Pass
  -> VS / PS / CS + render-state slice
  -> D3D11 buffers/resources/state
  -> draw
```

For the detailed version, including MODL/MLOD/VRTF/VBUF/IBUF and renderer examples, read:

```text
vfx/PACKAGE_TO_RENDER.md
```

## Models and Model Particles

Some particle data can render actual model geometry.

The validated bounded route includes:

```text
Particle model reference
  -> MODL
    -> MLOD
      -> materialReference
      -> VRTF vertex format reference
      -> VBUF vertex data
      -> IBUF index data
```

The exact bounded renderer facts are documented in:

```text
vfx/RENDERER_REFERENCE.md
```

An instanced Model Particle control also uses a second per-instance vertex stream with a proven 124-byte stride and `DrawIndexedInstanced`.

Again:

```text
Model Particle renderer != authored ModelEffect family
```

## Materials, MATD and MTST

Model geometry can reference a material directly or through a material-set resource.

The maintained resource graph distinguishes:

```text
mesh materialReference
-> MATD directly

or

mesh materialReference
-> MTST
-> one or more serialized MTST entries
-> MATD leaves
```

A faithful parser must retain the serialized MTST entries instead of silently choosing whichever variant looks convenient. The documented fixture populations for direct MATD routes, MTST routes and material leaves are centralized in `GAME_VERSION.md`.

Material data can then contain ShaderData fields and resource keys for textures or other resources. Missing references remain explicit source-data outcomes; they are not silently replaced with a guessed texture.

## Materials and PRECOMP

A material/resource path helps select shaders, textures and state, but not every private material field name has been recovered for the validated representation.

One example is the first field of the validated DX11 outer effect-like record: it is strongly correlated with known MATD Shader hashes, including an exact equality on a maintained fixture, but the maintained template does **not** rename it as a proven MATD field because a universal causal selector relationship has not been established.

This is intentional. A technically useful tool can preserve exact numeric/resource identities without pretending every private name is known.

---

# Why can we find some EA names, but not others?

When studying *The Sims 4*, we can often understand **exactly what the game is doing** even when we cannot recover the **original name EA gave to a function, field or class**.

The key idea is:

```text
Knowing what something does
!=
Knowing what EA originally called it
```

## Source code versus the compiled game

Imagine a developer originally wrote something conceptually like:

```cpp
class SomeShakeInterface
{
    void ApplyShake(float strength);
};
```

That is only an illustration, **not a claim about an EA class name**.

After source code is compiled into the release `TS4_x64.exe`, the CPU does not need source identifiers such as:

```text
ApplyShake
strength
mShakeStrength
```

It mainly needs executable facts such as:

```text
object address
memory offset
function address
arguments
```

So the executable may only let us prove something equivalent to:

```text
object + offset
-> vtable
-> virtual function at another offset
-> called with a float value
```

That can be enough to reproduce the behavior even if the original source spelling disappeared completely.

## 1. RTTI and surviving class names

Sometimes useful C++ RTTI (Run-Time Type Information) survives compilation.

Examples recovered during this research include names such as:

```text
EA::Swarm::cParticlesEffect
EA::Swarm::cParticlesDescription
EA::Swarm::cRibbonDescription
EA::Swarm::cRibbonEffect
EA::Swarm::cSequenceEffect
EA::Swarm::cBeamEffect
EA::Swarm::cGameEffect
```

When an RTTI identity is correctly attached to the object being analyzed, it gives us strong first-party naming evidence. We can then describe an offset relative to the proven class instead of only saying “unknown object”.

RTTI presence by itself still does not magically name every member inside the class.

## 2. Text strings the game must keep

Some names survive because the program needs the literal text at runtime.

Examples of VFX command vocabulary recovered in the validated research include strings such as:

```text
volume
length
density
texture
aspect
rotate
onStart
onStop
```

If the executable compares input against a literal command name and the handler then writes or consumes a particular field, that can establish a very strong semantic bridge:

```text
command string
-> command/parser path
-> exact storage
-> runtime consumer
```

That is much stronger than guessing from a value that merely “looks like” volume, width or lifetime.

## 3. Named command/runtime classes can help

Sometimes a field itself has no surviving member name, but a proven surrounding type, parser or command class gives semantic context.

First-party examples recovered and correctly attached in the validated executable include names such as:

```text
cParticleAlignmentCommand
cParticlePhysicsCommand
cMetaParticleDirectedWalkCommand
cShakeAmplitudeCommand
cShakeFrequencyCommand
cDistributeSourceCommand
cRibbonWidthCommand
cRibbonTaperCommand
cRibbonSlipCurveCommand
cDecalMapEmitColorCommand
```

The safe rule is that the name must be **actually attached by evidence** to the path being described. A plausible command name must never be published as an EA identity merely because it sounds right.

Even a real command-class name does not automatically reveal the private member name or enum spelling of every value it touches. For example, a proven alignment command does not by itself recover private symbolic names for every serialized alignment value.

## 4. Sometimes only the behavior survives

It is possible to prove a branch such as:

```text
specific bit/value
-> exact runtime branch
-> exact visible or mechanical consequence
```

while the original EA enum/member name remains unavailable.

The correct documentation style is then:

```text
mechanical behavior:       PROVEN
original EA semantic name: UNKNOWN
```

Do **not** turn the observation into a made-up private name such as `KillParticlesFlag` just because that would be convenient.

## Why source names disappear

The Sims 4 ships as an optimized **Release build**, not as EA's original Visual Studio project.

Compilation/linking can remove or transform information the running program does not need, including:

- local variable names;
- parameter names;
- private function names;
- structure member names;
- enum names;
- typedef names;
- comments;
- debug information;
- intermediate helper structures;
- separate functions that were inlined.

For example, source code might conceptually contain:

```cpp
float mSomeStrength;
```

while the final executable only needs an access equivalent to:

```cpp
*(float *)(object + 0x34)
```

If the string `mSomeStrength` is not present in any surviving metadata or external symbol information, a reverse-engineering tool cannot recover that exact spelling from the machine instruction alone.

## What is `FUN_14196FE30`?

Names such as:

```text
FUN_14196FE30
FUN_141998AD0
FUN_141930530
```

are generally **analysis-tool labels created by Ghidra**, not EA source names.

They effectively mean:

```text
there is a function at/around this address,
but Ghidra does not know its original source name
```

The numeric address portion is also **build-specific**. A `FUN_...` label or RVA used as evidence for the documented executable must not be treated as a stable API or assumed to identify the same function after a game update.

A research project may later assign a descriptive label after proving the function's role. That descriptive label still must not be presented as the original EA C++ identifier unless independent first-party evidence establishes it.

## Compiler optimization makes this harder

Source like:

```cpp
float volume = description->volume;
PlaySound(volume);
```

can compile into something much closer to:

```asm
movss xmm2, [rcx+offset]
call some_function
```

The source word `volume` may be gone even though the dataflow is still perfectly traceable.

So we can reach:

```text
MECHANICAL BEHAVIOR = PROVEN
ORIGINAL EA NAME    = UNKNOWN
```

That is still valuable and often sufficient to build a faithful parser or viewer.

## Inlining can remove whole functions

A tiny source helper such as:

```cpp
void SetWidth(float width)
{
    mWidth = width;
}
```

may be inlined into every caller. The separate function and its source name may no longer exist in the executable at all.

If a surviving command string/parser path independently connects `width` to the resulting storage and consumer, the **meaning** can still be recovered even though the original setter function name cannot.

## The named object may belong to another engine system

A SWARM effect can call into another part of The Sims 4 engine.

For example, a ShakeEffect can be mechanically traced to a retained external endpoint and a virtual call that consumes the computed strength. That lets us describe the behavior as a shake endpoint.

If the external interface has no surviving RTTI/name, we must not claim EA called it something convenient such as `ICameraShake`.

In our own application code, a clear descriptive name such as `ShakeEndpoint` is fine **as long as it is documented as our name rather than EA's**.

## The main naming rule

For this project:

```text
Real EA name found and correctly attached
-> use it

Behavior/dataflow proven but EA name missing
-> describe the behavior

Neither proven
-> keep it unknown
```

Something can therefore be mechanically understood while its original private naming remains unknown and still be perfectly usable for reproducing the effect.

The priority is reproducing the real behavior, not guessing what EA's private developers happened to call every field, enum, function or interface.

For the formal evidence/provenance vocabulary used in these resources, read:

```text
precomp/guides/LABEL_PROVENANCE.md
```

---

# Binary inspection tools

## What is 010 Editor?

010 Editor is a commercial hex/binary editor. Its Binary Template language lets a `.bt` text file describe structures, arrays and fields in a binary file.

```text
EA .precomp file
  + matching .bt template
  + 010 Editor
  -> structured human inspection
```

Maintained `.bt` files:

```text
precomp/templates/010-editor/
```

## What is ImHex?

ImHex is a free/open-source hex editor with its own Pattern Language (`.hexpat`).

Maintained patterns:

```text
precomp/templates/imhex/
```

ImHex does not execute 010 Editor `.bt` files; the two languages are separate implementations of the same documented validation boundaries.

## Why not put the whole decoder in the editor templates?

Human inspection and exhaustive validation have different needs.

The project needed to prove the complete DX11 source-record -> physical-stream -> Raw-Snappy -> DXBC path across the full documented corpus. Dedicated parser/validation tooling is more appropriate for that work than embedding a second decompressor in each editor template.

The `.bt` and `.hexpat` files therefore stay focused on readable, bounded structural inspection.

## Why are some fields `Unknown`, `Raw` or `WordN`?

Because a size, offset, pointer rule or physical role can be known without the original private EA member name being known.

The rule is simple: **do not turn plausibility into a field name**.

## Validated versus historical templates

The validated DX11 file and Win32 Version 2 file are separate formats.

Historical material under `precomp/old-references/` is preserved because older first-party/community templates contain useful vocabulary and structures, but old offsets/names are not copied into a newer representation without proof against the relevant build/bytes.

See:

```text
precomp/guides/LEGACY_TEMPLATES.md
```

## Safe first PRECOMP experiment

1. Read `GAME_VERSION.md` and compare your game/PRECOMP fingerprint with the documented fixture.
2. Copy `Shaders_DX11.precomp` from your own installation to a working folder.
3. Choose 010 Editor or ImHex.
4. Open the copied `.precomp`.
5. Run the matching maintained `.bt` or `.hexpat`.
6. Expand the exposed `DATA` / `shaders` structures.
7. Compare build-specific populations with `GAME_VERSION.md` and structural details with `precomp/guides/FORMAT_REFERENCE.md`.
8. If your hash differs, treat it as a compatibility check rather than weakening warnings immediately.

---

# FAQ

## Is this a mod?

No. The PRECOMP templates and these documents are inspection/research resources. They do not go in `Mods`.

## Do the templates contain EA shaders?

No. The maintained templates do not redistribute the installed PRECOMP binary or extracted DXBC corpus.

## Do I need 010 Editor?

No. TS4 Animation Browser does not need it. For manual inspection, ImHex is the free/open-source alternative included here.

## Can the templates work after a game patch?

Possibly. A changed hash means the bytes differ from the exact validated fixture; it does not automatically mean the structure changed. Use `GAME_VERSION.md` as the fixture reference and revalidate the affected boundary before claiming full compatibility.

## Does “mechanically closed” mean every EA name is known?

No. It means the required storage/dataflow/endpoint transitions for the stated scope are bounded, with remaining private semantic unknowns explicitly classified rather than hidden.

Build-specific closure metrics belong in `GAME_VERSION.md`; they are not duplicated here.

## Where do I go next?

```text
game build + fingerprints:  GAME_VERSION.md
PRECOMP overview:           precomp/README.md
PRECOMP binary details:     precomp/guides/FORMAT_REFERENCE.md
label/evidence provenance:  precomp/guides/LABEL_PROVENANCE.md
complete VFX pipeline:      vfx/PACKAGE_TO_RENDER.md
authored families:          vfx/AUTHORED_FAMILIES.md
named research fixtures:    vfx/TESTED_EFFECT_FIXTURES.md
renderer/GPU layouts:       vfx/RENDERER_REFERENCE.md
VFX .swb/.swb2/.swh2:       vfx/RESOURCE_TOPOLOGY.md
change history:             CHANGELOG.md
```
