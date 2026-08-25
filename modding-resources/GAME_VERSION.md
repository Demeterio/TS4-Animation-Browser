# Validated The Sims 4 game version and research fixtures

This file is the **canonical version/fixture reference for `modding-resources/`**.

Other modding documents should link here instead of copying the validated research game build, patch date or PRECOMP fingerprints into multiple files.

## Validated game build

```text
Game:         The Sims 4
Platform:     PC / Windows
Game version: 1.126.73.1030
Patch date:   2026-07-23
```

Unless a document explicitly says otherwise, current-format counts and validation claims in `modding-resources/` refer to this research build/corpus.

The patch date above is the date associated with this The Sims 4 game version. It is **not** the filesystem timestamp of a PRECOMP file and it is not the date this documentation was written.

## Canonical-version rule

`GAME_VERSION.md` is the single canonical declaration of the game build, patch date and fingerprints used by the maintained documentation set.

There are two intentional exceptions where a build number may also appear:

1. **versioned template filenames**, because the filename identifies the snapshot for which that artifact was published;
2. **embedded `Validated fixture` comments inside `.bt` / `.hexpat` files**, because a copied standalone template should retain the build against which that exact artifact was tested.

Those artifact-local version strings describe the artifact's validation snapshot. They do not replace this file as the canonical documentation-wide fixture declaration.

When the validated research build changes, update this file first. Rename or edit a template artifact only if that artifact is actually revalidated/published for the new build.

## Validated corpus anchors

The main build-specific anchors used by the VFX/PRECOMP documentation are:

```text
Client packages enumerated                         258
Effective modern VisualEffects resources        12,430
Effective VisualEffectsInstanceMap resources         1
Effective VisualEffectsMerged resources              1
VisualEffect definitions                        33,852
Authored-family references                     116,967
Active authored families                            11
Modern VisualEffects decode failures                 0

DX11 compact shader streams                     37,960
Vertex shaders                                   7,947
Pixel shaders                                   30,009
Compute shaders                                      4
```

These values are validation anchors for this corpus. They are not universal constants that a parser should hard-code for future game builds.

## PRECOMP file fingerprints

These are the exact PRECOMP files used during the maintained PRECOMP investigation. No game PRECOMP files are distributed with these resources.

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

## Check your installed PRECOMP files

On Windows PowerShell:

```powershell
Get-FileHash "<The Sims 4>\Game\Bin\res\Shaders_DX11.precomp" -Algorithm SHA256
Get-FileHash "<The Sims 4>\Game\Bin\res\Shaders_Win32.precomp" -Algorithm SHA256
```

If the SHA-256 matches, the file is byte-for-byte identical to the documented PRECOMP fixture.

If it does not match, that proves only that the bytes differ. It does **not** automatically prove that the binary format changed or that a maintained template is unusable.

## Compatibility rule for another game patch

A different The Sims 4 build is a **compatibility boundary** until the relevant data is checked.

For PRECOMP, minimally compare:

1. file magic/version and size;
2. SHA-256 or another exact fingerprint;
3. established counts, strides and relative-pointer bounds;
4. pass shader-reference behavior;
5. source-record -> physical compact-stream mapping when the DX11 package changed;
6. Raw Snappy -> DXBC reconstruction for affected shader streams.

For VFX/SWARM, compare the effective DBPF resource set and reopen only the structures/families whose bytes or runtime behavior in the new build contradict the documented model.

Do not weaken parser checks merely because a newer game file is different.

## Where this file is referenced

The main entry points are:

```text
README.md
BEGINNER_GUIDE.md
precomp/README.md
precomp/guides/FORMAT_REFERENCE.md
vfx/README.md
vfx/TESTED_EFFECT_FIXTURES.md
CHANGELOG.md
```

Editor-specific guides should also link here for the exact research fixture.
