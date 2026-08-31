# Validated The Sims 4 game version and research fixture

This file is the **canonical build/fixture reference for `modding-resources/`**.

Other maintained modding documents should link here instead of copying the validated game version, build fingerprints, corpus populations, closure percentages or PRECOMP hashes into multiple files.

Durable format/route documentation may still contain structural constants that define the actual format or consumer behavior, such as record sizes, field offsets, state IDs, vertex strides and exact conversion rules.

## Validated game build

```text
Game:         The Sims 4
Platform:     PC / Windows
Game version: 1.126.73.1030
Patch date:   2026-07-23
```

Unless a document explicitly says otherwise, current-format counts and validation claims in `modding-resources/` refer to this research build/corpus.

The patch date above is the date associated with this The Sims 4 game version. It is **not** the filesystem timestamp of a PRECOMP file and is not the date this documentation was written.

## Build compatibility fingerprint

The maintained oracle uses an exact build fingerprint so a later game update cannot silently inherit build-specific proofs.

```text
Build ID:
1C9C27E4A85D68E73F3E1C41B73472F7DACAF7D6C2E4E7321D14537A98A042A4

TS4_x64.exe SHA-256:
F07EFD2A7036A2316FB47B735AF1C7B682BEDDE59D906A7C87E81F246A536F47

Shaders_DX11.precomp SHA-256:
734EA81E166BEFCDFF8A3C06E22BE4471900834920653A48AD89F9339FB4D638

Shaders_Win32.precomp SHA-256:
9ACFFAE8C7698C6AE232D4234A49F1FFC84E990556516FB1B7138DA1E8E509A3
```

The project Build ID fingerprints the game version plus the executable and maintained DX11/Win32 PRECOMP binaries. It is **not** a hash of every DBPF package in the installed corpus.

A matching Build ID therefore establishes the maintained binary compatibility anchor, while package/resource counts are checked separately by corpus validation.

## Canonical-version rule

`GAME_VERSION.md` is the single documentation-wide declaration of the validated game build, binary fingerprints and build-specific populations/coverage metrics.

Two intentional exceptions may retain a version string locally:

1. **versioned template filenames**, because the filename identifies the snapshot for which that artifact was published;
2. **embedded `Validated fixture` comments inside `.bt` / `.hexpat` files**, because a copied standalone template should retain the build against which that exact artifact was tested.

Those artifact-local strings describe the artifact's validation snapshot. They do not replace this file as the source of truth.

When the validated research build changes, update this file first. Rename or edit a template artifact only if that artifact is actually revalidated/published for the new build.

## Validated VFX/DBPF corpus anchors

```text
Client packages enumerated                         258
Indexed resource copies                      2,583,071
Effective resources                          1,912,035

Effective modern VisualEffects resources        12,430
Effective VisualEffectsInstanceMap resources         1
Effective VisualEffectsMerged resources              1
Effect identities / definitions                 33,852
Effect IID -> MasterIID mappings                33,852
Authored-family references                     116,967
Direct VisualEffect child edges                  9,802
Modern VisualEffects decode failures                 0
Active authored families                            11
```

Authored-family reference counts:

```text
0x00 VisualEffect            9,802
0x01 ParticleEffect         80,621
0x02 MetaparticleEffect     17,684
0x03 DecalEffect                23
0x04 SequenceEffect            497
0x05 SoundEffect             7,274
0x06 ShakeEffect                 9
0x0B GameEffect                  6
0x0D DistributeEffect            2
0x0E RibbonEffect            1,027
0x0F SpriteEffect               22
```

These are build-specific validation anchors, not constants a parser should hard-code for future patches.

## Particle resource-reference anchors

```text
ParticleEffect sections                         12,430
Exact ParticleEffect records                    35,346
Primary Particle resource/IID fields            35,346
ParticleEffect authored refs                    80,621
Particle model-route records                    12,019
Particle authored model refs                    17,578
Resolved MODL model routes                      11,948
Missing MODL model routes                           71
Ambiguous MODL model routes                          0
```

The exact-record and primary-field counts match because every decoded ParticleEffect record has exactly one primary resource field in the maintained current parser model.

The 71 missing model routes are classified source-data outcomes, not hidden parser failures.

Independent missing-MODL audit:

```text
Unique missing MODL IIDs                            60
Effective MODL tombstone IIDs                        8
IIDs with no indexed MODL copy                      52
IIDs with shadowed MODL copies                       8
Unindexed package MODL hits                          0
Package files inspected                          5,056
Indexed package files                              258
Unindexed package files                          4,798
```

Interpretation:

- an effective DBPF tombstone remains the override winner and is not live content;
- shadowed lower-priority MODL copies are not resurrected;
- same-instance resources under other type IDs are not substitutes for an authored MODL reference.

## Model / material / texture anchors

```text
MODL roots                                      2,749
MODL decode failures                                0
LOD references                                  5,304
Decoded MLOD                                    5,304
Imported meshes                                 5,303
Zero VRTF references                            2,307
Internal mesh resource refs                    18,905
External/missing/unsupported mesh refs              0

Mesh material references                        5,303
Direct MATD mesh routes                         5,271
MTST mesh routes                                   32
MTST entry routes                                 195
Normalized mesh -> MATD bindings                5,466
Unique MATD definitions                         5,464
Serialized MTST variants                          195
Non-zero MTST variants                            144
Non-zero MTST material states                      26
Unsupported material-binding routes                 0

MTRL ShaderData fields                         71,025
ShaderData resource keys                        7,467
Resolved ShaderData resource keys               7,427
Missing ShaderData resource refs                   40
Unique resolved DDS textures                    1,329
Texture metadata rows                           1,329
DST textures                                    1,311
Textures with known DXGI format                 1,329
Unresolved serialized texture formats               0
```

`Textures with known DXGI format` is a count of texture rows whose mechanically decoded metadata resolves to a DXGI format. It is **not** a count of 1,329 distinct DXGI enum values.

The 40 missing ShaderData resource references are classified source-data outcomes in this corpus. They are not hidden parser failures.

The currently missing material references include both previously audited absent resources and resources whose effective DBPF winner is a tombstone. No alternate type or shadowed resource is substituted.

## DX11 PRECOMP anchors

```text
Payload version                                     6
Effect-like records                            77,771
Techniques                                    210,355
Passes                                        175,016
Serialized render-state pairs                 122,417
Shader-preparation records                      3,237

Compact shader streams                         37,960
Vertex shaders                                  7,947
Pixel shaders                                  30,009
Compute shaders                                     4
```

All compact shader streams are mechanically mapped to their stage-source records and reconstruct as Raw Snappy -> DXBC for this fixture.

## Runtime / renderer oracle anchors

```text
Runtime types / active-family routes                 11
Renderer families                                     6
Global renderer routes                                2
Normalized manifest GPU fixtures                      6
Fixture vertex layouts                                6
Fixture vertex elements                              34
Normalized runtime/PRECOMP rows                      24
  SELECTION                                          18
  DIRECT_CALLBACK_PASS                                6
Fixture RenderDoc pipeline-state snapshots            6
```

The six normalized manifest GPU fixtures are a deliberately bounded manifest subset. `vfx/TESTED_EFFECT_FIXTURES.md` also documents additional named research controls that are not counted in that six-fixture normalized set.

Fixture observations remain fixture-scoped. They are regression evidence, not universal renderer defaults.

The maintained `male_child_blue` selection remains fixture-scoped:

```text
PRECOMP record 965
selector 2
pass 0
VSRef 4097
PSRef 19464
CSRef 0
state slice 71456 + 1
```

Do not promote this to a family-wide Model Particle rule.

## Closure metrics

Different evidence populations use different denominators and must not be merged into one invented global percentage.

### Authored-family pipeline — family-balanced view

```text
Mechanical closure        100.00%
Strict semantic-known      97.05%
UNKNOWN_BOUNDED             2.95%
Missing                     0.00%
```

### Authored-family pipeline — secondary authored-reference-weighted view

```text
Mechanical closure        100.00%
Semantic-known            ~97.54%
UNKNOWN_BOUNDED           ~ 2.46%
Missing                     0.00%
```

The family-balanced view prevents the large Particle population from dominating the status of much smaller authored families. The reference-weighted view is useful as a separate corpus-impact perspective.

### Generic DX11 PRECOMP state dispatcher

Exact denominator: state IDs `0x00..0x1E`.

```text
Mechanical closure         31 / 31 = 100.00%
Concrete D3D/mechanical    30 / 31 =  96.77%
UNKNOWN_BOUNDED purpose     1 / 31 =   3.23%
Missing                     0 / 31 =   0.00%
```

State `0x1B` has exact mechanical behavior but no proven private EA conceptual name. It is not missing implementation work.

### Implementation-critical research status

For the documented build, the maintained research/oracle has no known blocking mechanical edge for the currently modeled VFX pipeline.

That statement does **not** mean every private field, enum, class or original source-level name has been recovered. Remaining private semantics stay bounded where the mechanics are sufficient.

## PRECOMP file details

### `Shaders_DX11.precomp`

```text
Size:    80,158,711 bytes
SHA-1:   6DCFB85F1A389ECABDD616FC3EB23A7F46BFCA41
SHA-256: 734EA81E166BEFCDFF8A3C06E22BE4471900834920653A48AD89F9339FB4D638
```

Recorded file timestamp in the research installation:

```text
2026-06-28T03:33:42.2643870Z
```

### `Shaders_Win32.precomp`

```text
Size:    105,794,410 bytes
SHA-1:   3A1D6049BE78ABFB262D246100BE0702491635CD
SHA-256: 9ACFFAE8C7698C6AE232D4234A49F1FFC84E990556516FB1B7138DA1E8E509A3
```

Recorded file timestamp in the research installation:

```text
2026-06-28T03:34:45.2984474Z
```

## Check your installed binaries

PowerShell:

```powershell
Get-FileHash "<The Sims 4>\Game\Bin\TS4_x64.exe" -Algorithm SHA256
Get-FileHash "<The Sims 4>\Game\Bin\res\Shaders_DX11.precomp" -Algorithm SHA256
Get-FileHash "<The Sims 4>\Game\Bin\res\Shaders_Win32.precomp" -Algorithm SHA256
```

A matching hash means the file is byte-for-byte identical to the documented fixture. A different hash proves only that the bytes differ; it does **not** automatically prove that the format changed.

## Compatibility rule for another game patch

A different The Sims 4 build is a **compatibility boundary** until the affected facts are checked.

For VFX/SWARM, compare the effective DBPF resource set and reopen only structures/families whose bytes or runtime behavior contradict the documented model.

For PRECOMP, minimally compare:

1. file magic/version and size;
2. exact fingerprints;
3. table counts, strides and relative-pointer bounds;
4. pass shader-reference behavior;
5. source-record -> physical compact-stream mapping when the DX11 package changed;
6. Raw Snappy -> DXBC reconstruction for affected shader streams;
7. state dispatcher behavior if the executable changed.

For renderer/runtime facts, treat a changed executable hash as a boundary for RVA/dataflow claims even if the serialized resource format is unchanged.

Do not weaken parser checks merely because a newer game file is different.

## Main references

```text
README.md
BEGINNER_GUIDE.md
precomp/README.md
precomp/guides/FORMAT_REFERENCE.md
precomp/guides/LABEL_PROVENANCE.md
precomp/guides/TOOLS_AND_METHOD.md
vfx/README.md
vfx/AUTHORED_FAMILIES.md
vfx/PACKAGE_TO_RENDER.md
vfx/RENDERER_REFERENCE.md
vfx/TESTED_EFFECT_FIXTURES.md
vfx/RESOURCE_TOPOLOGY.md
CHANGELOG.md
```
