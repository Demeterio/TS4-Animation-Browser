# Historical PRECOMP references

This folder preserves older PRECOMP Binary Templates that were useful during the research leading to the maintained current TS4 templates.

They are **historical references**, not maintained current templates. Their authorship, game generation and validation level are different from the templates under `../templates/`.

The reference files are kept as close to their original form as possible. Inherited comments or old assumptions are part of their historical context and must not be mistaken for newly validated current TS4 semantics.

## The Sims 4 — original Win32 Version 1 reference

File in this repository:

```text
TS4/TheSims4-Shaders_Win32_SimGuruModSquad_2014-10-25.precomp.bt
```

### Origin

**Source:** EA Forums — *Shaders_Win32.precomp info*  
https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/shaders-win32-precomp-info/2215518

**Author / publisher:** SimGuruModSquad / Maxis (Electronic Arts)  
**Preserved publication/reference date:** October 25, 2014

The EA forum post states that `Shaders_Win32.precomp` contains the game shaders and provides a `.bt` Binary Template for the resource. It also documents a `Resource.cfg` method that could be used at the time to override the PRECOMP from a mod-side directory instead of replacing the installed file directly.

The template itself identifies its target as:

```text
The Sims 4
Copyright 2014 Electronic Arts Inc. All rights reserved.
010 Editor v5.0 Binary Template
Resource: Shaders_Win32.precomp version 1
```

It exposes a substantial amount of historical first-party vocabulary, including structures/tags such as:

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

This makes it an especially valuable first-party reference for the **old TS4 Win32/SPKG Version 1 generation**.

It is not a template for the modern DX11 `DATA` / `shaders` Version 6 file, and its deeper Version 1 layout must not be assumed byte-identical to the current Win32 Version 2 file without independent validation.

## The Sims 3 — community Win32 reference

File in this repository:

```text
TS3/TheSims3_Shaders_Win32_Lyralei_2020-01-03.precomp.bt
```

### Origin

**Source:** Mod The Sims — *Cracking Open Shaders_Win32.precomp? (for better shaders!)*, post by Lyralei  
https://modthesims.info/showthread.php?p=5600893#post5600893

**Authors:** Lyralei and Tashiketh (referred to as “Tash” in the post)  
**Published:** January 3, 2020

Lyralei explains in the original post that she and Tash made a byte-reading template for The Sims 3 by adapting the older TS4 `.bt` file. The post also explains why the beginning of the template still contains TS4 wording inherited from the source template.

That inherited TS4 header is therefore a **historical artifact of the adaptation**, not evidence that the TS3 template was authored by Electronic Arts or that it targets The Sims 4.

The authors documented important differences and unresolved questions at publication time, including:

- The Sims 3 using `mHeapSize` in places where the older TS4 behavior differed;
- the meaning/linkage of `mPlatform` entries still being investigated;
- the linkage between `mVertexShaderParams` / `mPixelShaderParams` and MLOD/MODL/package data still being investigated;
- the template being useful for reading and research without claiming that every relationship in the format had been solved.

The original post also states that the template was run with 010 Editor.

This file is preserved as a **community historical reference**. It targets The Sims 3 and should not be treated as current TS4 format proof.

## Why both references matter

The old TS4 template supplies valuable first-party historical names and layout information. The TS3 adaptation shows how community researchers used that knowledge to explore a related but different PRECOMP generation.

Neither should be judged only by the number of structures it displays. Their value is tied to the specific game/file generation they describe.

For the current The Sims 4 work, use the maintained templates under:

```text
../templates/010-editor/
../templates/imhex/
```

For a technical comparison of old TS4, TS3 and the maintained current TS4 templates, see:

```text
../guides/LEGACY_TEMPLATES.md
```

## Attribution rule

Do not relabel either historical file as Demeterio-authored material.

The TS4 reference keeps its Electronic Arts/Maxis attribution. The TS3 reference keeps the Lyralei/Tashiketh community attribution. Historical names and assumptions remain reference evidence only until the current game bytes or runtime behavior independently support them.
