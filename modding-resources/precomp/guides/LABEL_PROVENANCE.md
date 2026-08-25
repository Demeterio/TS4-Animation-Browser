# Label provenance and confidence policy

This document explains **where the names used in the maintained PRECOMP/VFX documentation come from**.

A readable name is not automatically an original EA private symbol. The goal is to make the distinction explicit enough that another modder or tool author can reuse the structural knowledge without accidentally turning a project label into folklore.

## Evidence categories

| Category | Meaning | Publication rule |
| --- | --- | --- |
| `EA_NAME_PROVEN` | current first-party string, RTTI/type identity, symbol-like metadata or other exact current EA identity is attached to the relevant object/resource | may be presented as an EA/current name within that scope |
| `DATAFLOW_PROVEN` | exact producer -> storage -> consumer relationship proves the role | publish the role, but do not invent the stripped member name |
| `PHYSICAL_SEMANTIC_PROVEN` | GPU slot/format/stride/shader consumption or another physical endpoint proves the meaning | publish the physical interface fact |
| first-party historical | name exists in preserved EA/Maxis material for an older representation | useful vocabulary; current transfer requires independent proof |
| historical/community | name/layout exists in a community tool/reference | candidate/corroboration only |
| project structural/descriptive | project name for a structure/role whose mechanics are proven but private EA name is unknown | publish only as a project/descriptive label |
| `UNKNOWN_BOUNDED` | mechanics/storage/dataflow are sufficiently constrained, but exact private name/finer semantic is not proven | preserve raw/numeric form and state the boundary |
| `CONTEXT_ONLY` | behavior requires live gameplay/service context outside a standalone inspection path | do not misclassify as a missing renderer |

## PRECOMP labels

| Label | Provenance/status | Notes |
| --- | --- | --- |
| `DATA` | current bytes | file/root tag observed in the current DX11 PRECOMP |
| `shaders` | current bytes | current root-entry string |
| payload version `6` | current bytes + loader behavior | validated current DX11 shader payload version |
| `Effect` / `Dx11EffectRecord` | project structural label | proven outer record hierarchy; stripped current EA private type name is not claimed |
| `Technique` | current structure + first-party historical vocabulary | current nesting/dataflow independently proven |
| `Pass` | current structure + first-party historical vocabulary | current nesting/dataflow independently proven |
| `VSRef` / `PSRef` / `CSRef` | `DATAFLOW_PROVEN` role labels | 1-based stage reference, `0` = absent |
| `StateStart` / `StateCount` | `DATAFLOW_PROVEN` role labels | select a bounded state-pair slice; private member names not claimed |
| `StatePairBoundRaw` | project descriptive label | bounds every referenced current state slice; finer/private meaning unknown |
| `ShaderPreparationRecord` | project structural label | size/table role bounded; internal fields intentionally raw |
| source record `+0x00` physical stream locator | `DATAFLOW_PROVEN` | exact source-record -> Raw Snappy physical stream relation; private member name not asserted |
| Raw Snappy | physical storage format proven | all 37,960 compact streams reconstruct to DXBC |
| DXBC | physical compiled-shader format proven | reflected/disassembled/created through D3D11 |

## Win32 PRECOMP labels

The preserved EA/Maxis Version 1 template supplies first-party historical vocabulary such as:

```text
PlatformShader
Technique / TECH
Pass / PASS
ShaderParam / PARM
Sampler
render-state structures
```

For the maintained current Win32 Version 2 prefix, names including:

```text
PlatformShader
IsPixelShader
ShaderPlatformMask
ShaderSize
ByteCodeSize
ByteCode
```

have historical first-party vocabulary behind them **and** the Version 2 layout/mechanics are independently revalidated.

The current Version 2 `SPAR` body is deliberately different in publication policy: its two exposed 32-bit words remain:

```text
Word0
Word1
```

The richer Version 1 semantics are not transferred automatically.

## Current VFX resource names and extensions

These are current executable mappings and may be treated as EA current names within the documented resource-routing scope:

```text
0x1B192049  Sims4EffectsOpt  .swb2
0x1B19204A  Sims4EffectsOptH .swh2
0xEA5118B0  Sims4Effects     .swb
```

The configuration key:

```text
SwarmDisableCollectionStreaming
```

is also a current EA string. Its exact branch dataflow is proven.

The descriptive phrase “split/streaming route” versus “merged fallback/non-streaming route” is project wording used to explain the proven branch. It is not a recovered EA class name.

## Authored-family names

The maintained family labels are used for the typed modern SWARM records whose current structure/runtime identity is established by the project corpus and current executable analysis.

Current active labels:

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

Current runtime EA identities are published where separately proven, for example:

```text
EA::Swarm::cParticlesDescription
EA::Swarm::cParticlesEffect
EA::Swarm::cRibbonDescription
EA::Swarm::cRibbonEffect
EA::Swarm::cBeamEffect
cSequenceEffect
cSequenceDescription
cGameEffect
cGameDescription
cSubdivOwner
```

Do not infer an EA runtime class name merely from an authored family label when that identity has not been independently attached.

## Renderer-family labels

Several useful renderer names in these docs are **maintained descriptive categories**, not assertions about EA private C++ class names.

| Label | Status |
| --- | --- |
| Classic particle/sprite | project renderer-family label backed by exact 24-byte IA layout and bounded draws |
| Model Particle | project renderer behavior label backed by MODL/MLOD geometry and current renderer dataflow |
| Model Particle instanced | descriptive sub-route backed by exact slot-1 124-byte instance stream and `DrawIndexedInstanced` |
| Dynamic `013f` | project renderer-family label for an exact bounded runtime/GPU route |
| Decal | authored/runtime family identity plus exact bounded dedicated renderer route |
| raw `0x83` route | literal authored value + bounded case-study classification; no universal semantic name |

This distinction matters because a descriptive renderer label can be **physically exact** without being the original private class name.

## Material/shader labels

A correlation is not promoted merely because it is strong.

Example:

```text
DX11 outer effect-like record +0x00
```

is strongly correlated with known MATD Shader hashes in current data, but the maintained template keeps the field structurally/raw named because the exact current semantic attachment has not been elevated to a proven private member identity.

Likewise, state-pair IDs/values are not renamed into guessed D3D11 enum/business names solely from plausible values.

## `UNKNOWN_BOUNDED` examples

Current examples that remain intentionally bounded include:

- several Particle private flag meanings;
- original/private names for alignment values `9..12`;
- some Ribbon formula/member names;
- Sequence trailing raw 4 bytes;
- some Sound raw fields/private service identities;
- Shake private endpoint/interface identity;
- Distribute non-zero nested Surface/SurfacePoint data without a current non-zero fixture;
- Sprite v5 constant 20-byte tail;
- private name of the Sprite returned renderer-side interface;
- VisualEffectsInstanceMap 18-byte post-table trailer;
- broader meanings of raw `0x83`/`0x8B` beyond exact fixtures;
- renderer/material record private names where only physical dataflow is exact.

These are not hidden parser failures. They are deliberately preserved boundaries.

## Historical/community sources

Historical templates and tools are valuable evidence when handled carefully.

The preserved EA/Maxis 2014 PRECOMP template is first-party evidence for its own old Win32 generation. TS4 VFX Tool provides substantial historical/community VFX/SWARM reader/writer structures and useful field-name candidates.

Neither source is automatically authoritative for a different current representation.

The evidence order used by maintained current docs is:

```text
current game bytes / current EA runtime behavior
-> exact dataflow / GPU physical role
-> first-party historical name when current transfer is independently supported
-> historical/community correlation
-> project structural label
-> UNKNOWN / UNKNOWN_BOUNDED
```

## Rule for templates and third-party tools

When reusing these names in another parser/template:

1. preserve the label's provenance class;
2. do not remove `Unknown`, `Raw`, `WordN` or numeric values merely because a plausible friendly name exists elsewhere;
3. if a future game build contradicts a current mapping, treat it as a version/format change until resolved;
4. distinguish parsing correctness from semantic naming completeness;
5. report unsupported/unknown data rather than silently coercing it into the nearest known structure.
