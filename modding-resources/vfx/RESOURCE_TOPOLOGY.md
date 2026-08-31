# VFX resource topology — `.swb`, `.swb2` and `.swh2`

Fixture: see `../GAME_VERSION.md`.

This document explains how the VFX library in the validated build is split across DBPF resource types and how that executable selects the split/streaming or merged representation.

Build-specific resource/identity populations are centralized in `../GAME_VERSION.md` rather than duplicated here.

## Resource types

Resource types established for the validated build:

```text
0x1B192049  VisualEffects
0x1B19204A  VisualEffectsInstanceMap
0xEA5118B0  VisualEffectsMerged
```

The executable from that build maps them to these names and extensions:

```text
0x1B192049  -> Sims4EffectsOpt  -> .swb2
0x1B19204A  -> Sims4EffectsOptH -> .swh2
0xEA5118B0  -> Sims4Effects     -> .swb
```

`VisualEffectsMerged` and `VisualEffectsInstanceMap` are different resources. The merged `.swb` is a VFX library; the `.swh2` resource is the instance/master mapping used by the split collection representation.

## Fixture-specific resource examples

The documented research fixture contains effective split `VisualEffects` resources plus one effective `VisualEffectsInstanceMap` and one effective `VisualEffectsMerged` resource. Exact populations belong in `../GAME_VERSION.md`.

The following exact TGIs are retained as **fixture examples**, not future-build constants:

```text
VisualEffectsMerged
  EA5118B0:0051185B:6F6664543F71A573

VisualEffectsInstanceMap
  1B19204A:0051185B:5A1E6D0162252B3E
```

Tools should resolve resources from the actual effective DBPF indexes instead of assuming that every future build will keep these instance IDs or package placement.

## Identity relationship

The merged library exposes two tail tables joined by the same raw 32-bit handle:

```text
raw handle -> effect name
raw handle -> Effect IID
```

The validated InstanceMap then provides:

```text
Effect IID -> MasterIID
```

`MasterIID` is the instance ID of the split `VisualEffects` resource that owns that effect identity.

The exact offline route is therefore:

```text
VisualEffectsMerged (.swb)
  raw handle -> effect name
  raw handle -> Effect IID
                    |
                    v
VisualEffectsInstanceMap (.swh2)
  Effect IID -> MasterIID
                    |
                    v
DBPF index
  0x1B192049:*:MasterIID
                    |
                    v
VisualEffects (.swb2)
```

This is an identifier-level relationship. It does **not** depend on effect-name equality.

## MasterIID does not have to equal Effect IID

Several effect identities can live in one split resource.

The strings below are **effect names from the validated VFX library**, not family names or labels added to the names:

```text
Effect name: ability_transform_sparkle_trail
  EffectIID = A82981BE8E0DAA86
  MasterIID = A82981BE8E0DAA86

Effect name: ability_transform_sparkle_trail_02
  EffectIID = E3CBD43969E5E457
  MasterIID = A82981BE8E0DAA86
```

Both identities resolve to:

```text
1B192049:0051185B:A82981BE8E0DAA86
```

A tool that assumes `EffectIID == resource instance` will therefore fail for valid effects in the documented corpus.

## Complete validated identity audit

The current audit establishes, for the fixture in `../GAME_VERSION.md`:

```text
every merged Effect IID is present in the InstanceMap
every mapped MasterIID resolves to an effective split VisualEffects resource
no conflicting MasterIID mapping is present
```

Safe conclusion for the documented corpus:

> No effect identity serialized in `VisualEffectsMerged` is known to exist only in the merged library.

This does not mean the merged resource is dead or unused.

## Executable selection in the validated build

The executable from the validated build contains an explicit `VisualEffectsMerged` resource lookup route.

The EA configuration string:

```text
SwarmDisableCollectionStreaming
```

selects between the two resource-library strategies observed at initialization:

```text
SwarmDisableCollectionStreaming == false
  -> collection-streaming / split + InstanceMap-side route

SwarmDisableCollectionStreaming == true
  -> VisualEffectsMerged / Sims4Effects / .swb route
```

The config call supplies `0` as its default requested value. That proves the default passed by this callsite; it does **not** prove that no external/shipping configuration can override it.

The phrase “merged fallback/non-streaming route” is descriptive project wording for this branch, not an asserted private EA class name.

## What a modern tool should normally do

For content matching the documented fixture, the split path is the useful primary representation:

```text
requested Effect IID
  -> InstanceMap MasterIID
  -> effective 0x1B192049 VisualEffects resource
  -> locate the requested VisualEffect definition inside that resource
```

This preserves the exact EA identity relation and avoids relying only on effect-name deduplication.

A separate decoder for the merged resource may still be useful for historical/compatibility work, but it is not required merely to recover identities that are absent from the validated split library: the current audit found none.

## InstanceMap trailing bytes

After the validated mapping table, the documented InstanceMap contains 18 trailing bytes:

```text
00000000000000000001000000000000FFFF
```

The exact private meaning is not proven. The correct classification is:

```text
UNKNOWN_BOUNDED
```

Do not invent a field name based on visual similarity to another SWARM trailer. The trailer does not block complete decoding of the validated identity table.

## Tool-author checklist

When resolving an effect:

1. read the effective DBPF resource set rather than only one hard-coded package;
2. preserve the full TGI identity;
3. use `Effect IID -> MasterIID` when the InstanceMap is available;
4. do not require `Effect IID == MasterIID`;
5. treat missing map entries, missing MasterIID targets and conflicting mappings as explicit errors/classes rather than silently guessing;
6. keep the 18-byte post-table trailer raw until its meaning is independently proven;
7. do not assume the presence of a merged resource means it contains unique identities.

Named identity examples and renderer/runtime fixtures used during the research are collected in `TESTED_EFFECT_FIXTURES.md`.
