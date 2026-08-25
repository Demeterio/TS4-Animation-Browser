# Named VFX research fixtures

Fixture: see `../GAME_VERSION.md`.

This document lists the **named effects deliberately retained as representative research controls, captures, runtime observations or resource-identity examples** in the maintained VFX work.

It is not a list of every effect touched by automated corpus scans. The corpus-wide tools inspected thousands of resources and generated larger differential shortlists; those automated candidates are not all promoted here as named reference fixtures.

The purpose of this table is narrower: make it clear which concrete The Sims 4 effects were repeatedly used to connect authored SWARM data, runtime behavior, renderer paths, PRECOMP shaders and resource identity.

## How to read the family column

The authored-family column records only relationships that were actually established for that fixture.

```text
known authored family/composition                    -> listed
renderer known but authored family not used as proof -> not asserted
child/reachable family                               -> explicitly marked as reachable
identity-only example                                -> no renderer/family inference
```

A renderer family must never be used to guess an authored family. For example, a Classic-looking draw does not by itself prove `ParticleEffect` or `SpriteEffect`.

## Renderer and GPU controls

| Effect name | Authored family/composition used as evidence | Research role | Evidence retained | Safe conclusion |
| --- | --- | --- | --- | --- |
| `clue_sparkle` | `ParticleEffect` | Classic particle/sprite control | SWARM decode + RenderDoc/3Dmigoto + CPU/runtime bridge | six Particle blocks feed the bounded Classic 24-byte dynamic-geometry route for the studied effect |
| `ep1_clue_sparkle` | `VisualEffect` + `SoundEffect`; child reaches the visible Classic path | recursive/child-effect and sound-bearing control | SWARM graph + renderer capture | a similarly named effect can be composition/orchestration rather than the same authored body as `clue_sparkle` |
| `make_it_rain_c` | `ParticleEffect` | Classic comparison/control | SWARM comparison + GPU capture | reaches the same broad Classic shader/layout family as `clue_sparkle` while using different blend behavior in the studied captures |
| `beam_bright_blue` | not asserted from this control | Classic renderer control | GPU capture/control corpus | useful Classic/beam-looking renderer control; the renderer observation alone is not used to assign an authored family |
| `accursed_hand_04` | `ParticleEffect` with model behavior | Model Particle, non-instanced | SWARM/resource resolution + MODL/MLOD + GPU capture | uses real model geometry rather than a billboard/sprite fallback |
| `ep05_flowers_table_hand_chrysanthemum_white` | `ParticleEffect` with model behavior | Model Particle, non-instanced comparison | resource resolution + MODL/MLOD + GPU capture | second non-instanced Model Particle control with different geometry |
| `male_child_blue` | `ParticleEffect` with model behavior | Model Particle, instanced reference control | MODL/MLOD + exact 124-byte slot-1 map + shader dataflow + `DrawIndexedInstanced` | authoritative bounded fixture for the per-instance `0x7C` stream |
| `discoball_ground_dots_decal` | `DecalEffect` | Decal renderer control | authored/runtime Decal path + GPU capture | reaches the bounded dedicated indexed Decal route |
| `ability_transform_sparkle_trail` | `RibbonEffect` | Dynamic `013f` control | authored Ribbon data + runtime/GPU route + PRECOMP correlation | at least this Ribbon fixture reaches Dynamic `013f`; this is not generalized to every Ribbon |
| `gp07_mother_plant_spray` | `ParticleEffect` | Dynamic `013f` comparison | authored Particle data + runtime/GPU route | demonstrates that a Particle fixture can converge on the same Dynamic `013f` renderer family as a Ribbon fixture |
| `ep12_pom_high_skill_fail01_left` | `ParticleEffect`; includes authored `drawMode = 0x83` records and model records | raw-`0x83` / Model Particle control | authored inspection + installed MLOD-to-captured-IA exact match + `DrawIndexed(1152)` | the captured geometry follows the ordinary Model Particle/MLOD route; no distinct `0x83` geometry renderer is established |

## Authored composition and side-effect controls

| Effect name | Authored family/composition used as evidence | Research role | Evidence retained | Safe conclusion |
| --- | --- | --- | --- | --- |
| `lightning_flash` | `GameEffect` | gameplay/context-side authored control | authored outcome graph + manual playback observation | authored effect can produce a visible event while containing a non-geometry GameEffect path |
| `lightning_flash_full` | `VisualEffect` + `SoundEffect` + `ShakeEffect`; child `lightning_flash` reaches `GameEffect` | combined visual/audio/shake control | authored outcome graph + manual playback observation | one VFX can combine child orchestration, sound and shake behavior instead of mapping to one renderer |
| `ep18_world_4spout_fountain` | `VisualEffect` ×4 + `SoundEffect`; reachable `ParticleEffect` | combined visual/audio child graph | authored outcome graph + manual playback observation | visible jets and sound can come from different authored branches of the same effect graph |
| `lightning_summon_projectile` | `VisualEffect` + `ParticleEffect`; reachable `SequenceEffect` | Sequence reachability/composition control | serialized authored graph/outcome analysis | Sequence behavior can be reached through an authored graph rather than requiring a unique renderer identity |
| `droid_r1_display_lights` | `DistributeEffect` references include the single observed serialized Distribute record | context-dependent / Distribute control | authored rare-family targeting + manual Effect Player observation | no visible Effect Player output does not prove inactivity; object/gameplay/context dependencies remain possible |

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

## What this fixture set proves — and what it does not

The named controls deliberately cover several different questions:

```text
Classic dynamic geometry
Model Particle non-instanced
Model Particle instanced
Dynamic 013f
Decal
raw authored drawMode 0x83 case
child VisualEffect graphs
Sequence reachability
sound
shake
gameplay/context behavior
Distribute/context behavior
split/merged resource identity
```

They do **not** imply that every effect in the game was manually captured or played. Corpus-wide structural claims come from the complete automated resource/PRECOMP validation described elsewhere, while these named fixtures provide representative end-to-end anchors for specific runtime and renderer questions.

When adding a future fixture, record why it was selected and which evidence type it supplies. Do not add an authored-family label only because the final image or renderer path looks similar to another control.
