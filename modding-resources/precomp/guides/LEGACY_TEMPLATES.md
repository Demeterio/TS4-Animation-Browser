# Current and historical PRECOMP templates

This document explains how the maintained current The Sims 4 templates relate to the older TS4 and TS3 Binary Templates preserved under `../old-references/`.

For the exact current The Sims 4 research build, patch date and PRECOMP fingerprints, use the canonical fixture file:

```text
../../GAME_VERSION.md
```

For exact authors, publication dates and source links for historical material, see:

```text
../old-references/README.md
```

For current naming/evidence rules, see:

```text
FORMAT_REFERENCE.md
```

## Short answer: are the current templates better?

It depends on the target:

- **Current The Sims 4 DX11:** yes, decisively. The maintained work targets the modern `DATA` / `shaders` Version 6 package and its important relations were mechanically validated across the complete shader corpus.
- **Historical TS4 Win32 Version 1:** the old SimGuruModSquad/Maxis template remains the richer first-party description of its own format generation and preserves valuable original EA vocabulary.
- **The Sims 3:** the Lyralei/Tashiketh community template is useful historical research for another game, but its authors explicitly documented unresolved relationships.

More names or more lines do not automatically mean a template is more accurate. Target file generation and evidence matter more.

## Files being compared

### Maintained TS4 DX11

010 Editor:

```text
../templates/010-editor/010Editor_TheSims4_Shaders_DX11_v1.126.73.1030_2026-08-21.precomp.bt
```

ImHex:

```text
../templates/imhex/ImHex_TheSims4_Shaders_DX11_v1.126.73.1030_2026-08-21.hexpat
```

The version token in these filenames identifies the published template snapshot. The canonical game-build/date/fingerprint declaration remains `../../GAME_VERSION.md`.

Target:

```text
The Sims 4 Shaders_DX11.precomp
DATA / shaders payload Version 6
```

### Maintained TS4 Win32 Version 2

010 Editor:

```text
../templates/010-editor/010Editor_TheSims4_Shaders_Win32_v1.126.73.1030_2026-08-21.precomp.bt
```

ImHex:

```text
../templates/imhex/ImHex_TheSims4_Shaders_Win32_v1.126.73.1030_2026-08-21.hexpat
```

Again, the version token belongs to the artifact filename; use `../../GAME_VERSION.md` for the authoritative current research fixture metadata.

Target:

```text
The Sims 4 Shaders_Win32.precomp
SPKG Version 2
```

The maintained publication boundary deliberately stops at:

```text
SPKG -> SHDB -> LINK -> PlatformShader[] -> DBUF -> SPAR -> SMDB
```

### Historical TS4 Win32 Version 1

```text
../old-references/TS4/TheSims4-Shaders_Win32_SimGuruModSquad_2014-10-25.precomp.bt
```

The original template identifies itself as an Electronic Arts 2014 010 Editor v5.0 Binary Template for `Shaders_Win32.precomp version 1`.

It contains a much larger old Win32 model including:

```text
SHDB
LINK
DBUF
SPAR
SMPS
SETS
SSET
SHDS
SBLK
TECH
PASS
PARM
KNM
END
```

Its historical `PASS` records expose valuable first-party names such as vertex/pixel shader code IDs, parameter lists, render states, samplers and dynamic state.

That vocabulary is excellent evidence for the **old Version 1 representation**. It is not automatic proof for modern DX11 or the unvalidated tail of current Win32 Version 2.

### Historical TS3 community template

```text
../old-references/TS3/TheSims3_Shaders_Win32_Lyralei_2020-01-03.precomp.bt
```

Lyralei and Tashiketh adapted the older TS4 `.bt` to read the Sims 3 PRECOMP. The inherited TS4 header at the start of that file is therefore a historical artifact of the adaptation.

Their publication notes explicitly left several relationships open, including `mPlatform` and the connection of vertex/pixel shader parameters to MLOD/MODL/package data.

It is therefore a **community historical reference**, not a first-party EA TS3 specification and not a modern TS4 template.

## Side-by-side comparison

| Area | Historical TS4 Win32 v1 | Historical TS3 community adaptation | Current TS4 Win32 v2 | Current TS4 DX11 |
| --- | --- | --- | --- | --- |
| Target game | TS4 | TS3 | TS4 | TS4 |
| Target generation | old Win32/SPKG v1 | TS3 Win32-era PRECOMP | SPKG v2 | modern DX11 DATA/v6 |
| Origin | SimGuruModSquad / Maxis | Lyralei + Tashiketh | Demeterio research | Demeterio research |
| Old `TECH/PASS` vocabulary | first-party | inherited/adapted | intentionally not claimed below current boundary | current nesting/dataflow independently proven; `Technique`/`Pass` terminology additionally has first-party historical provenance |
| Outer modern `Effect` label | not the current DX11 representation | no direct current-TS4 role | not applicable to maintained prefix | project structural label for the proven current outer record, not asserted as the stripped EA type name |
| Shader storage proof | historical bytecode fields | historical/community investigation | platform-shader prefix validated | Raw Snappy streams -> exact DXBC validated across the documented corpus |
| Full-corpus automated validation | not part of the historical `.bt` | no | prefix/counts validated | yes for the documented corpus |
| VS/PS/CS source record -> physical shader proof | no modern equivalent | no | not the DX11 model | yes across all documented stage records |
| D3D11 reflection/shader-creation validation | no | no | not the DX11 target | yes |
| Current TS4 DX11 usefulness | historical context | no direct current-TS4 role | secondary Win32 path | primary current template |

Exact current corpus counts belong to `../../GAME_VERSION.md` rather than being duplicated here.

## Where the historical TS4 template is stronger

The Version 1 template has a major advantage as a historical reference: it preserves **first-party EA/Maxis vocabulary** directly in the structure definitions.

That gives us names for old shader blocks, techniques, passes, shader parameters, render-state data and samplers. Those names help understand how an earlier renderer package was organized.

We preserve that file rather than pretending the modern research makes its historical value obsolete.

First-party historical provenance is also useful when a current structure independently matches. For example, `PlatformShader`, `IsPixelShader`, `ShaderPlatformMask`, `ShaderSize` and `ByteCodeSize` are historical EA vocabulary and the maintained Version 2 prefix independently revalidates the corresponding current layout/mechanics.

The opposite case is equally important: the old Version 1 `SPAR` fields have meaningful first-party names, but the maintained Version 2 SPAR record keeps `Word0`/`Word1` because those semantics have not yet been transferred mechanically.

## Where the current DX11 work is stronger

For modern The Sims 4 DX11, the maintained research targets the file the current D3D11 renderer actually uses and tests the important relationships mechanically.

The following chain is proven inside the publication boundary:

```text
effect-like outer record (project structural label)
-> Technique
-> Pass
-> 1-based stage source reference
-> stage source record
-> exact Raw Snappy compact stream
-> exact DXBC
```

Every recovered VS/PS/CS source record in the documented fixture resolves to its exact physical stream. Reconstructed DXBC was checked using Direct3D reflection/disassembly and D3D11 shader creation. Representative real renderer/VFX draws were also cross-checked with RenderDoc, 3Dmigoto and targeted CPU/runtime analysis.

That is a substantially stronger basis for a **modern TS4 DX11 tool** than transplanting an old Version 1 Win32 structure because its names happen to be convenient.

The word `Effect` is kept as a project navigation/structural label for the current outer record. The current pointer/count/dataflow proof does not depend on claiming that the stripped modern EA C++ type is literally named `Effect`.

## Why current Win32 Version 2 is shorter than the old Version 1 template

This is deliberate.

The observed current Win32 file reports Version 2. The research mechanically revalidated:

```text
SPKG -> SHDB -> LINK -> PlatformShader[] -> DBUF -> SPAR -> SMDB
```

but did not prove the entire Version 1 tail below that boundary is byte-for-byte unchanged.

The maintained Version 2 templates therefore stop at `SMDB` instead of copying `TECH/PASS/PARM` and turning historical information into an unsupported current claim.

A shorter template can be **more reliable** when it clearly separates known structure from assumption.

## Why DX11 is not a replacement for Win32

The modern DX11 file uses a separate `DATA` / `shaders` Version 6 structure. Findings from DX11 are not copied into Win32 simply because both packages contain shaders.

Each file is validated independently against its own bytes and loader behavior.

## TS4 VFX Tool boundary

TS4 VFX Tool is useful historical/community evidence for authored VFX/SWARM layouts and names. It is not a first-party PRECOMP specification and is not used to promote modern PRECOMP field names.

When TS4 VFX Tool and PRECOMP research meet, the useful comparison is at the **bridge from authored VFX data to renderer/material/pass selection**, not by assuming that similarly named fields belong to the same binary format.

## Future research rule

If a future pass mechanically validates the Win32 Version 2 `SMDB` tail, the maintained Win32 templates can be extended one structure at a time.

Historical names may be reused only where current bytes and behavior independently support them.

The evidence priority remains:

```text
current game bytes / EA runtime behavior
-> exact dataflow / GPU role
-> first-party historical name when mechanically transferable
-> TS4 VFX Tool / community historical correlation
-> project structural label
-> UNKNOWN
```

Never promote a historical or community label into the maintained current templates only because it sounds plausible.
