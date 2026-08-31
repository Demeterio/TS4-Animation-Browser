# Named VFX research fixtures

Fixture: see `../GAME_VERSION.md`.

This document lists the **named effects deliberately retained as representative research controls, captures, runtime observations or resource-identity examples** in the maintained VFX work.

It is not a list of every effect touched by automated corpus scans. The corpus-wide tools inspected thousands of resources and generated larger differential shortlists; those automated candidates are not all promoted here as named reference fixtures.

The purpose of this document is narrower: make it clear which concrete The Sims 4 effects were repeatedly used to connect authored SWARM data, runtime behavior, renderer paths, PRECOMP shaders and resource identity.

## Scope rule

```text
fixture observation -> fixture fact
route observation   -> route fact
corpus audit        -> corpus fact
```

Do not promote a fixture-specific layout, PRECOMP record or blend state to a family-wide rule without independent evidence.

## How to read the authored-family column

The authored-family column records only relationships that were actually established for that fixture.

```text
known authored family/composition                    -> listed
renderer known but authored family not used as proof -> not asserted
child/reachable family                               -> explicitly marked as reachable
identity-only example                                -> no renderer/family inference
```

A renderer family must never be used to guess an authored family. For example, a Classic-looking draw does not by itself prove `ParticleEffect` or `SpriteEffect`.

Likewise, an observed renderer route or PRECOMP record is not automatically an exclusive property of the named fixture. Runtime observations retain their fixture/build/scope in the evidence model.

## Renderer and GPU controls

| Effect name | Authored family/composition used as evidence | Research role | Evidence retained | Safe conclusion |
| --- | --- | --- | --- | --- |
| `clue_sparkle` | `ParticleEffect` | Classic particle/sprite control | SWARM decode + RenderDoc/3Dmigoto + CPU/runtime bridge + targeted PRECOMP ownership controls | Particle blocks feed the bounded Classic dynamic-geometry route for the studied effect; unique named PRECOMP ownership remains `UNKNOWN_BOUNDED` |
| `ep1_clue_sparkle` | `VisualEffect` + `SoundEffect`; child reaches the visible Classic path | recursive/child-effect and sound-bearing control | SWARM graph + renderer capture | a similarly named effect can be composition/orchestration rather than the same authored body as `clue_sparkle` |
| `make_it_rain_c` | `ParticleEffect` | Classic comparison/control | SWARM comparison + GPU capture | reaches the same broad Classic layout/shader family as `clue_sparkle` while using different blend behavior in the studied captures |
| `beam_bright_blue` | not asserted from this control | Classic renderer control | GPU capture/control corpus | useful Classic/beam-looking renderer control; the renderer observation alone is not used to assign an authored family |
| `accursed_hand_04` | `ParticleEffect` with model behavior | Model Particle, non-instanced | SWARM/resource resolution + MODL/MLOD + GPU capture | uses real model geometry rather than a billboard/sprite fallback |
| `ep05_flowers_table_hand_chrysanthemum_white` | `ParticleEffect` with model behavior | Model Particle, non-instanced comparison | resource resolution + MODL/MLOD + GPU capture | second non-instanced Model Particle control proving layout variation |
| `male_child_blue` | `ParticleEffect` with model behavior | Model Particle, instanced reference control | MODL/MLOD + exact 124-byte slot-1 map + runtime PRECOMP attribution + `DrawIndexedInstanced` | authoritative bounded fixture for the per-instance stream and exact fixture PRECOMP bridge |
| `discoball_ground_dots_decal` | `DecalEffect` | Decal end-to-end renderer/PRECOMP control | authored/runtime ownership + exact PRECOMP selection/pass probes + exact stream writers/binders + GPU history | investigated route is mechanically closed as runtime Decal -> PRECOMP selection/pass -> `FUN_140B11640` -> slot0 stride 44 + slot1 stride 4 + 16-bit IB -> `DrawIndexed`; a separate historical `16/16` RenderDoc event is real but is **not** mechanically owned by this exact route |
| `ability_transform_sparkle_trail` | `RibbonEffect` | Dynamic `013f` control | authored Ribbon data + runtime/GPU route + PRECOMP correlation + targeted PRECOMP ownership controls | at least this Ribbon fixture reaches Dynamic `013f`; this is not generalized to every Ribbon and unique named PRECOMP ownership remains `UNKNOWN_BOUNDED` |
| `gp07_mother_plant_spray` | `ParticleEffect` + `SoundEffect` in the established authored composition | Dynamic `013f` comparison | authored Particle data + runtime/GPU route + targeted PRECOMP ownership controls | establishes at least one current Particle-authored `drawMode 0x8B` route to Dynamic `013f`; this is not generalized to all ParticleEffect records and unique named PRECOMP ownership remains `UNKNOWN_BOUNDED` |
| `ep12_pom_high_skill_fail01_left` | `ParticleEffect`; includes authored `drawMode = 0x83` records and model records | raw-`0x83` / Model Particle geometry-identity control | authored inspection + installed MLOD-to-captured-IA exact comparison + GPU capture | captured geometry follows the ordinary Model Particle/MLOD route; no distinct `0x83` geometry renderer or unique authored draw owner is established |

## Exact GPU fixture notes

### `clue_sparkle`

Purpose: Classic renderer control.

A proven capture uses the Classic 24-byte `POSITION/COLOR/TEXCOORD` IA layout.

Its PRECOMP candidate ranking never constituted unique ownership proof. The final targeted runtime ownership campaign repeatedly observed one configured Classic-compatible pair:

```text
PRECOMP record 53275
selector       1
```

Its producer provenance was mechanically classified through the common transient route and matched the `record+0x18` context candidate. However, the same pair was also continuously active in surrounding control windows:

```text
trigger windows: 90, 129, 101
control windows: 60, 93, 118, 92, 73, 63
```

The selected record also carries:

```text
raw +0x00 = 0x6DA87A9B
```

which is the maintained hash associated with the current `particle` context family.

Safe conclusion:

```text
Classic-compatible PRECOMP context observed     YES
exclusive clue_sparkle ownership                NO
unique clue_sparkle -> record 53275 mapping     NOT PROVEN
classification                                  UNKNOWN_BOUNDED
```

The observation is consistent with a shared/background Particle context. It is not normalized as an exclusive named-effect PRECOMP route.

### `make_it_rain_c`

This separate Classic control is useful because similar geometry/shader characteristics do not imply identical output-merger state.

Bounded comparison:

```text
clue_sparkle   -> SrcAlpha + One
make_it_rain_c -> SrcAlpha + InvSrcAlpha
```

This is why fixture pipeline-state snapshots are regression evidence rather than global Classic state definitions.

### `accursed_hand_04`

```text
DrawIndexed(210)
slot0 stride 20
separately bound slot1 stride 16
R16_UINT index buffer
```

The capture proves this fixture renderer state/layout, not exact authored-block ownership or unique PRECOMP selection.

### `ep05_flowers_table_hand_chrysanthemum_white`

```text
DrawIndexed(423)
slot0 stride 24
separately bound slot1 stride 16
R16_UINT index buffer
TEXCOORD1 present
```

Together with `accursed_hand_04`, this prevents a false single-stride/layout rule for non-instanced Model Particles.

### `ep12_pom_high_skill_fail01_left`

Installed-resource geometry identity control:

```text
DrawIndexed(1152)
slot0 stride 24
separately bound slot1 stride 16
R16_UINT index buffer
1,152 captured index rows
432 unique captured indices
1,170 installed model-mesh comparisons
1,170 exact captured-input matches
13,824 compared components per comparison
0 index mismatches
0 attribute mismatches
```

Interpretation boundary:

```text
installed geometry identity  PROVEN
unique authored draw owner   UNKNOWN_BOUNDED
```

The raw authored `drawMode = 0x83` seen in the investigated fixture is not promoted to a distinct renderer family.

### `male_child_blue`

IA/submission:

```text
slot1 per-instance stride 124
DrawIndexedInstanced
```

Exact fixture-scoped runtime PRECOMP attribution:

```text
record     965
selector   2
pass       0
VSRef      4097
PSRef      19464
CSRef      0
state      71456 + 1
observed   246 times in accepted scope
```

The selected record has:

```text
PRECOMP record 965 raw+0x00  = 0x0CB82EB8
current MATD.shader_key      = 0x0CB82EB8
```

This is a fixture-scoped physical equality, not proof that MATD directly selects record 965 or that all Model Particle effects use this PRECOMP row.

### `ability_transform_sparkle_trail`

This fixture proves a Ribbon-authored route to Dynamic `013f`.

It does **not** establish:

```text
RibbonEffect -> Dynamic013f globally
```

The maintained shader/pass candidate set for the targeted named-ownership campaign contained:

```text
53276 / selector 1
53278 / selector 1
68592 / selector 1
68594 / selector 1
```

Three repeated trigger rounds and their surrounding controls produced zero selections from that configured candidate set.

Safe conclusion:

```text
Dynamic 013f renderer/shader evidence           retained
configured candidate records                    retained as structural/shader-compatible candidates
exclusive named runtime PRECOMP ownership       NOT PROVEN
classification                                  UNKNOWN_BOUNDED
```

This negative ownership result does not invalidate the Dynamic `013f` renderer family, recovered shader hashes, PRECOMP pass structure or the independently proven source-table -> Raw Snappy -> DXBC -> 1-based stage-reference mapping.

### `gp07_mother_plant_spray`

This is the complementary Particle-authored Dynamic `013f` control.

Current evidence establishes:

```text
authored context includes ParticleEffect and SoundEffect
known renderer: Dynamic013f
drawMode 0x8B: at least one current Dynamic013f route proven
```

The same targeted candidate set used for the Dynamic ownership campaign:

```text
53276 / selector 1
53278 / selector 1
68592 / selector 1
68594 / selector 1
```

also produced zero selections across three repeated trigger rounds and their surrounding controls for this fixture.

Therefore:

```text
exclusive named runtime PRECOMP ownership  NOT PROVEN
classification                             UNKNOWN_BOUNDED
```

Together with `ability_transform_sparkle_trail`, the fixture still demonstrates that authored family and renderer family are not the same classification. The bounded PRECOMP ownership result does not weaken that renderer evidence.

## Named PRECOMP ownership rule retained from these fixtures

For the documented build, the final targeted result is:

```text
clue_sparkle
  unique named PRECOMP ownership = UNKNOWN_BOUNDED

ability_transform_sparkle_trail
  unique named PRECOMP ownership = UNKNOWN_BOUNDED

gp07_mother_plant_spray
  unique named PRECOMP ownership = UNKNOWN_BOUNDED
```

The targeted campaign is closed for this research fixture. No PRECOMP row is promoted from shader compatibility, event proximity or effect-name heuristics.

The durable rule is:

```text
renderer compatibility != exclusive named ownership
```

Reopen one of these named ownership questions only if a future implementation requirement or changed game build provides a new exact effect/block-to-runtime selection edge.

## Decal fixture scope note

For `discoball_ground_dots_decal`, two different evidence classes must stay separate.

Mechanically closed investigated route:

```text
Decal runtime owner/input
-> PRECOMP selection producer
-> PRECOMP record + selector
-> exact direct-callback pass when selector 1
-> FUN_140B11640
-> slot 0 stride 44
-> slot 1 stride 4
-> 16-bit index buffer
-> DrawIndexed
```

Historical RenderDoc observation:

```text
fixture active
-> real GPU event with 16/16 input configuration
-> exact Decal ownership not established
```

The targeted causality/parent-scope audits do not support a `44/4 -> 16/16` transformation or bridge. Do not model one.

The exact runtime selection closure observed multiple PRECOMP producer/record/selector combinations in the investigated scope, and the direct-callback selector-1 probe resolved a bounded set of exact pass routes. Neither result proves global or exclusive ownership of those PRECOMP records by this named effect.

See `RENDERER_REFERENCE.md` and `PACKAGE_TO_RENDER.md` for the public route details.

## Authored composition and side-effect controls

| Effect name | Authored family/composition used as evidence | Research role | Evidence retained | Safe conclusion |
| --- | --- | --- | --- | --- |
| `lightning_flash` | `GameEffect` | gameplay/context-side authored control | authored outcome graph + manual playback observation | authored effect can produce a visible event while containing a non-geometry GameEffect path |
| `lightning_flash_full` | `VisualEffect` + `SoundEffect` + `ShakeEffect`; child `lightning_flash` reaches `GameEffect` | combined visual/audio/shake control | authored outcome graph + manual playback observation | one VFX can combine child orchestration, sound and shake behavior instead of mapping to one renderer |
| `ep18_world_4spout_fountain` | `VisualEffect` children + `SoundEffect`; reachable `ParticleEffect` | combined visual/audio child graph | authored outcome graph + manual playback observation | visible jets and sound can come from different authored branches of the same effect graph |
| `lightning_summon_projectile` | `VisualEffect` + `ParticleEffect`; reachable `SequenceEffect` | Sequence reachability/composition control | serialized authored graph/outcome analysis | Sequence behavior can be reached through an authored graph rather than requiring a unique renderer identity |
| `droid_r1_display_lights` | `DistributeEffect` references include the observed serialized Distribute record | context-dependent / Distribute control | authored rare-family targeting + manual Effect Player observation | no visible Effect Player output does not prove inactivity; object/gameplay/context dependencies remain possible |

These controls are useful because a VFX tool must preserve non-GPU and orchestration branches instead of treating “no dedicated draw” as a parser failure.

## Resource identity / split-library examples

These examples were used to prove the `Effect IID -> MasterIID -> split VisualEffects resource` relationship. They are identity examples, not a claim that both effects were separately used as renderer captures.

| Effect name | Effect IID | MasterIID | Research role |
| --- | --- | --- | --- |
| `ability_transform_sparkle_trail` | `A82981BE8E0DAA86` | `A82981BE8E0DAA86` | identity maps directly to a split resource with the same instance ID |
| `ability_transform_sparkle_trail_02` | `E3CBD43969E5E457` | `A82981BE8E0DAA86` | proves that an Effect IID can differ from the MasterIID of the split resource that contains it |

Both identities resolve to the split resource:

```text
1B192049:0051185B:A82981BE8E0DAA86
```

## Fixture states versus generic PRECOMP states

These are two different evidence layers:

```text
RenderDoc fixture snapshot
    = exact state observed for one captured draw

common PRECOMP state dispatcher
    = generic engine mapping from serialized state IDs/values to D3D11 state
```

The generic mapping was recovered from the common EA consumer and does not depend on these named effects.

The manifest validator enforces fixture-state isolation so a capture cannot silently become a global renderer rule.

## What this fixture set proves — and what it does not

The named controls deliberately cover several different questions:

```text
Classic dynamic geometry
Model Particle non-instanced
Model Particle instanced
Dynamic 013f from more than one authored-family context
Decal runtime -> PRECOMP -> indexed draw
raw authored drawMode 0x83 case
child VisualEffect graphs
Sequence reachability
sound
shake
gameplay/context behavior
Distribute/context behavior
split/merged resource identity
```

They do **not** imply that every effect in the game was manually captured or played. Corpus-wide structural claims come from complete automated validation, while these named fixtures provide representative end-to-end anchors for specific runtime and renderer questions.

## When to add a new fixture

Add a new named fixture when it proves a materially new boundary, for example:

- a new renderer family or layout shape;
- a contradiction to an existing route invariant;
- an exact PRECOMP attribution not represented by current fixtures;
- a state/geometry variation needed as a regression gate;
- a future game build whose changed behavior needs a controlled comparison.

Do not add fixtures merely to increase a coverage number.

When adding a fixture, record why it was selected and which evidence type it supplies. Do not add an authored-family label only because the final image or renderer path looks similar to another control, and do not promote a fixture-level renderer/PRECOMP observation into a family-wide rule.
