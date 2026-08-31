# Label provenance and confidence policy

This document explains **where the names used in the maintained PRECOMP/VFX documentation come from**.

A readable name is not automatically an original EA private symbol. The goal is to make the distinction explicit enough that another modder or tool author can reuse the structural knowledge without accidentally turning a project label into folklore.

For the exact build and build-specific corpus anchors, see `../../GAME_VERSION.md`.

## Evidence categories

| Category | Meaning | Publication rule |
| --- | --- | --- |
| `EA_NAME_PROVEN` | current first-party string, RTTI/type identity, symbol-like metadata or other exact current EA identity is attached to the relevant object/resource | may be presented as an EA/current name within that scope |
| `DATAFLOW_PROVEN` | exact producer -> storage -> consumer relationship proves the role | publish the role, but do not invent the stripped member name |
| `PHYSICAL_SEMANTIC_PROVEN` | GPU slot/format/stride/shader consumption or another physical endpoint proves the meaning | publish the physical interface fact |
| `CORRELATED` | values or identities match strongly, but the causal/private semantic attachment is not independently established | publish as a correlation, not as a selector/member-name claim |
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
| `StateId` / `Value` | project structural labels + downstream `DATAFLOW_PROVEN` mapping | exact 8-byte serialized pair; public D3D destination comes from the common consumer, not from private member-name recovery |
| `StatePairBoundRaw` | project descriptive label | bounds every referenced current state slice; finer/private meaning unknown |
| `ShaderPreparationRecord` | project structural label | size/table role bounded; internal fields intentionally raw |
| source record `+0x00` physical stream locator | `DATAFLOW_PROVEN` | exact source-record -> Raw Snappy physical stream relation; private member name not asserted |
| Raw Snappy | physical storage format proven | validated compact shader storage route; build-specific population belongs in `GAME_VERSION.md` |
| DXBC | physical compiled-shader format proven | reflected/disassembled/created through D3D11 |

## State-pair semantics: structural name versus consumer role

Earlier documentation correctly refused to infer D3D meanings from plausible raw values or a few RenderDoc captures. That rule still stands.

What changed is the evidence source: the common EA PRECOMP state consumer has been recovered and mechanically establishes the destination/transformation for dispatcher IDs `0x00..0x1E`.

It is therefore valid to publish mechanical descriptions such as:

```text
state 0x10 -> rasterizer fill mode
state 0x11 -> rasterizer cull mode
state 0x16 -> render-target-0 source blend factor
```

because those descriptions come from current consumer dataflow into public D3D11 structures.

This does **not** mean the original private EA state-symbol names were recovered. The serialized pair can stay structurally named `StateId` / `Value` while `FORMAT_REFERENCE.md` documents what the consumer does with each supported ID.

### State `0x1B`

Canonical public structural key:

```text
BlendState.State0x1B
```

Status split:

```text
mechanical behavior     DATAFLOW_PROVEN
private conceptual name UNKNOWN_BOUNDED
```

The exact blend-descriptor transform is documented in `FORMAT_REFERENCE.md`. A convenient source-style name such as `AlphaBlendMode` or `UseAlphaFactors` would still be an invention unless first-party evidence appears.

### State `0x0F`

Canonical public structural key:

```text
DepthStencil.State0x0F
```

The consumer proves a front/back depth-stencil copy behavior when false. The project keeps this structural state identifier instead of presenting a guessed EA-private name.

### Exact enum and float conversions

The public numeric destinations/translators for comparison, stencil operations, blend factors and blend operations are `DATAFLOW_PROVEN`.

Likewise:

- `0x13` raw bits -> float32 behavior is exact;
- `0x14` context-dependent scaling -> int32 depth bias is exact;
- `0x1A` byte-to-float factor uses the exact float32 rounding of `1/255`.

These are public mechanical descriptions, not private symbol recovery.

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

Current runtime EA identities are published where separately proven. Examples include:

```text
EA::Swarm::cParticlesDescription
EA::Swarm::cParticlesEffect
EA::Swarm::cDecalDescription
EA::Swarm::cDecalEffect
EA::Swarm::cSequenceDescription
EA::Swarm::cSequenceEffect
EA::Swarm::cSoundDescription
EA::Swarm::cSoundEffect
EA::Swarm::cShakeEffect
EA::Swarm::cGameDescription
EA::Swarm::cGameEffect
EA::Swarm::cDistributeDescription
EA::Swarm::cDistributeEffect
EA::Swarm::cSubdivOwner
EA::Swarm::cRibbonDescription
EA::Swarm::cRibbonEffect
EA::Swarm::cBeamEffect
```

Current command classes/strings can also be published when correctly attached to their command path. Examples include Particle, Metaparticle, Shake, Distribute, Ribbon and Decal command classes documented in `../../vfx/AUTHORED_FAMILIES.md`.

Do not infer an EA runtime class/member name merely from an authored family label or from a neighboring command whose identity is known.

## Renderer-family labels

Several useful renderer names in these docs are **maintained descriptive categories**, not assertions about EA private C++ class names.

| Label | Status |
| --- | --- |
| Classic particle/sprite | project renderer-family label backed by exact IA layout and bounded draws |
| Model Particle | project renderer behavior label backed by MODL/MLOD geometry and current renderer dataflow |
| Model Particle instanced | descriptive sub-route backed by exact slot-1 instance stream and `DrawIndexedInstanced` |
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

is strongly correlated with known MATD Shader hashes in current data, including an exact equality on a maintained fixture, but the maintained template keeps the field structurally/raw named because a universal causal/private semantic attachment has not been proven.

Likewise, the existence of a real VFX command class does not authorize transplanting that name into a PRECOMP member or material field.

## `UNKNOWN_BOUNDED` examples

Current examples that remain intentionally bounded include:

- private names for some Particle enum/flag values despite a known consumer/command context;
- Sequence trailing raw bytes whose current private semantic name is not attached;
- Shake retained endpoint/interface identity;
- Sprite returned renderer-side interface private type name;
- VisualEffectsInstanceMap post-table trailer;
- broader meanings of raw renderer/draw-mode values beyond exact fixtures;
- material/renderer private names where only exact resource/dataflow behavior is known;
- PRECOMP state `0x1B` private conceptual name despite exact mechanical behavior.

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

## Stop rule

If the bytes, destination and transformation are sufficient for faithful implementation and the only missing information is the original private source spelling, keep the semantic bounded rather than inventing a name.
