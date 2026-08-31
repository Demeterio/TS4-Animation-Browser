# ImHex — The Sims 4 PRECOMP patterns

This folder contains native **ImHex Pattern Language** ports for the maintained The Sims 4 PRECOMP validation boundaries.

ImHex is a free/open-source hex editor and binary-analysis tool. Its Pattern Language is a C++/Rust-inspired language for describing binary data as structured records instead of reading only raw hexadecimal bytes.

These files are supplementary research resources. **ImHex is not required to run TS4 Animation Browser.**

If shaders, PRECOMP or binary templates are new to you, start with:

```text
../../../BEGINNER_GUIDE.md
```

For the exact validated game build, patch date and PRECOMP fingerprints, use:

```text
../../../GAME_VERSION.md
```

For the matching 010 Editor templates and guide:

```text
../010-editor/README.md
```

## Official ImHex links

Project:

https://github.com/WerWolv/ImHex

Releases:

https://github.com/WerWolv/ImHex/releases

ImHex documentation:

https://docs.werwolv.net/imhex

Pattern Language documentation:

https://docs.werwolv.net/pattern-language

Pattern Editor documentation:

https://docs.werwolv.net/imhex/views/pattern-editor

Pattern Data documentation:

https://docs.werwolv.net/imhex/views/pattern-data

## Is ImHex free?

Yes.

ImHex is free/open-source software. The main ImHex project is distributed primarily under the **GPL-2.0-only** license, with some project libraries/components using LGPL licensing as documented by the ImHex project.

There is no paid license required to use these `.hexpat` files.

At the time this documentation was prepared, the latest stable ImHex release was:

```text
ImHex 1.38.1
```

Nightly/pre-release builds may also be available. These PRECOMP patterns do not intentionally depend on a nightly build; for normal testing, start with the current stable Windows release.

## Installing ImHex on Windows

For Windows, the simplest route is the official ImHex Releases page:

https://github.com/WerWolv/ImHex/releases

Use the normal Windows-x86_64.msi installer/package appropriate for your system.

At the time this documentation was prepared, the official ImHex `v1.38.1` release notes state that the Windows installers are signed. Obtain ImHex from the official project/release page rather than from an unrelated download mirror.

After installation, launch ImHex normally. You do not need to copy these patterns into the ImHex installation directory to perform the basic manual workflow below.

## Patterns included here

### DX11

```text
ImHex_TheSims4_Shaders_DX11_vX_YYYY-MM-DD.hexpat
```

Use it with a working copy of:

```text
The Sims 4\Game\Bin\res\Shaders_DX11.precomp
```

The maintained publication boundary is:

```text
DATA
-> shaders payload v6
-> outer effect-like record
-> Technique
-> Pass
-> 1-based VS / PS / CS source reference
-> stage source records
```

The corresponding research also proves the source-record-to-physical-stream route through Raw Snappy to DXBC, but the display pattern deliberately does not duplicate the project's tested Raw Snappy decoder.

### Win32 Version 2

```text
ImHex_TheSims4_Shaders_Win32_vX_YYYY-MM-DD.hexpat
```

Use it with a working copy of:

```text
The Sims 4\Game\Bin\res\Shaders_Win32.precomp
```

The maintained boundary is intentionally limited to:

```text
SPKG -> SHDB -> LINK -> PlatformShader[] -> DBUF -> SPAR -> SMDB
```

The historical Version 1 `TECH/PASS/PARM` tail is not imported into the validated Version 2 pattern without independent mechanical validation.

## Runtime validation in ImHex

Both maintained `.hexpat` files were manually executed in **ImHex** against the fixture identified in `../../../GAME_VERSION.md`.

This is an actual Pattern Language runtime test, not only a syntax review against the ImHex documentation.

The exact values below are retained as results of that editor/template validation snapshot. `../../../GAME_VERSION.md` remains the canonical source for the current documentation-wide corpus populations.

### DX11 runtime result

The DX11 pattern completed with:

```text
Pattern exited with code: 0
```

The generated Pattern Data reported the expected fixture values:

```text
PayloadVersion = 6
EffectCount    = 77,771
VSCount        = 7,947
PSCount        = 30,009
CSCount        = 4
```

The generated table sizes also matched their validated record counts and strides, including:

```text
7,947  × 0x34 =   413,244 bytes  VertexShaderSourceRecords
30,009 × 0x2C = 1,320,396 bytes  PixelShaderSourceRecords
4      × 0x2C =       176 bytes  ComputeShaderSourceRecords
77,771 × 0x1C = 2,177,588 bytes  Effects
```

The selected navigation helper was then expanded in Pattern Data and correctly resolved:

```text
SelectedEffect
-> SelectedEffectTechniques
-> SelectedTechnique
-> SelectedPasses
```

The observed addresses independently confirmed the maintained field-relative pointer rule:

```text
target = address_of(relative_field) + relative_value
```

The selected pass exposed in-range 1-based shader references, with `CSRef = 0` for an absent compute stage.

### Win32 runtime result

The Win32 Version 2 pattern also completed with:

```text
Pattern exited with code: 0
```

Pattern Data reached the complete maintained boundary:

```text
SPKG
-> SHDB
-> LINK
-> PlatformShader[]
-> DBUF
-> SPAR
-> SMDB
```

The observed fixture values included:

```text
Version                  = 2
PlatformShaderCount      = 27,894
ShaderParameterCount     = 661
ShaderModelDatabaseTag   = "SMDB"
```

No Pattern Language runtime error was reported before the maintained `SMDB` boundary.

The measured evaluation times shown during these tests are machine-dependent and are therefore not treated as format guarantees or performance benchmarks.

These runtime results validate the ImHex representation of the **published structural boundaries** on the documented fixture. They do not extend the claim to later game builds with different PRECOMP hashes, nor do they turn still-unknown private fields into known semantics.

## Before you start

Do **not** experiment on the only copy inside the game installation.

Make a copy of the PRECOMP file you want to inspect first.

Example working files:

```text
C:\TS4-PRECOMP-Test\Shaders_DX11.precomp
C:\TS4-PRECOMP-Test\Shaders_Win32.precomp
```

The goal is inspection. There is no reason to risk changing the installed game files while learning the editor.

## Quick start — open and run a pattern

The basic workflow is:

1. Start **ImHex**.
2. Use **File -> Open File...** and open your copied `.precomp` file.
3. Open the **Pattern Editor** view.
4. Open/load the matching `.hexpat` file from this folder into the Pattern Editor.
5. Press the **Play** button in the Pattern Editor to execute the pattern.
6. Open the **Pattern Data** view if it is not already visible.
7. Expand the generated structures and inspect their values.
8. Click a pattern/field to correlate it with the corresponding bytes in the Hex Editor.

ImHex's Pattern Editor parses and executes the Pattern Language source against the currently loaded data provider. The generated structures appear in Pattern Data.

## What you should see after running the DX11 pattern

With the correct `Shaders_DX11.precomp`, the pattern should accept the initial:

```text
DATA
```

magic and follow the validated root/payload relationship.

The main generated `Precomp` structure contains the header, descriptor table and the resolved `shaders` payload.

Inside the payload, the pattern exposes the validated tables including:

```text
Effects
VertexShaderSourceRecords
PixelShaderSourceRecords
ComputeShaderSourceRecords
ShaderPreparationRecords
StatePairs
```

The exact counts depend on the file being inspected. For the validated research fixture they are runtime-confirmed in ImHex as:

```text
7,947  VS
30,009 PS
4      CS
37,960 shaders total
```

## Selected DX11 Effect / Technique / Pass view

Near the top of the DX11 `.hexpat` are:

```c
#define SELECT_EFFECT 0
#define SELECT_TECHNIQUE 0
```

These constants control a convenience view that follows one selected:

```text
Effect -> Technique -> Pass[]
```

using the same field-relative pointer rule proven in the EA loader path and runtime-confirmed by the ImHex fixture test described above.

For your first test, leave both values at `0`.

Later, if you want to inspect another known effect/technique, change the constants and press **Play** again.

Changing these constants changes only the Pattern Language evaluation. It does not modify the PRECOMP file.

## What you should see after running the Win32 pattern

With the matching `Shaders_Win32.precomp`, the file should begin with:

```text
SPKG
```

and report Version 2 for the validated fixture.

The pattern then walks:

```text
SHDB
LINK
PlatformShader[]
DBUF
SPAR
SMDB
```

For the validated research file the observed shader population is:

```text
7,521  vertex shaders
20,373 pixel shaders
27,894 total PlatformShader records
```

The maintained ImHex fixture test runtime-confirmed `PlatformShaderCount = 27,894` and successful traversal through `SMDB`.

The pattern intentionally stops after the validated `SMDB` boundary. Reaching that boundary is expected behavior; it is not a missing implementation caused by the editor.

## Pattern Data view

The **Pattern Data** view is where ImHex displays structures generated by the executed Pattern Language source.

It provides a tree containing information such as:

```text
Name
Offset
Size
Type
Value
```

Expand structures with their disclosure arrow to inspect children.

Clicking a generated pattern in Pattern Data also helps correlate the interpreted field with its physical byte range in the Hex Editor.

This is the closest ImHex equivalent to navigating the structured results produced by a 010 Editor Binary Template.

## Pattern Editor Console and errors

The Pattern Editor includes a **Console** area. Use it when a pattern does not complete as expected.

Our patterns deliberately use errors/warnings rather than silently continuing through obviously invalid structure.

Typical messages can indicate:

- wrong PRECOMP file for the selected pattern;
- unexpected magic/tag;
- unexpected Win32 version;
- a relative pointer or table extending outside the file;
- a future game update that changed an established boundary;
- malformed/truncated input.

Do not remove a bounds check merely to make the pattern continue. If a check fails on a legitimate new game file, that difference is useful evidence and should be investigated.

## Do not enable Auto evaluate for the first full PRECOMP test

ImHex offers an **Auto evaluate** option that reruns Pattern Language whenever the source changes.

The official ImHex documentation recommends Auto evaluate mainly for small patterns because larger evaluations can cause noticeable performance issues while editing.

These TS4 PRECOMP files contain very large tables. For the first tests, leave Auto evaluate **off** and manually press **Play** after making a change.

That avoids repeatedly rebuilding tens of thousands of generated records while typing.

## Why the patterns raise ImHex evaluator limits

ImHex's Pattern Language protects itself with default limits on array sizes and generated pattern counts.

The documented defaults are much smaller than the real validated TS4 shader populations:

```text
ImHex default array_limit:   0x1000
ImHex default pattern_limit: 0x2000

Validated TS4 DX11 PS source records: 30,009
Validated TS4 Win32 PlatformShaders:  27,894
```

For that reason both maintained `.hexpat` files explicitly raise:

```text
#pragma array_limit ...
#pragma pattern_limit ...
```

Those values are **ImHex evaluator limits**, not claims about maximum PRECOMP record counts.

Do not reinterpret them as part of the EA file format.

## Performance expectations

The DX11 and Win32 PRECOMP files are large and the generated tables contain many records.

A full pattern evaluation is therefore heavier than opening a tiny binary format.

If ImHex remains responsive but takes some time to populate Pattern Data, that alone is not evidence of a parse failure. Watch the Pattern Editor state/Console and let the explicit pattern checks tell you whether a structural problem occurred.

For interactive exploration after the first successful run:

- keep Auto evaluate off;
- expand only the structures you need;
- use the DX11 selected Effect/Technique constants for focused inspection;
- avoid editing the underlying PRECOMP data unless you intentionally want to test modifications on a disposable copy.

## Raw Snappy and DXBC

The DX11 pattern does **not** decompress the compact shader streams.

That is intentional.

The project already validated the complete compact shader corpus with dedicated tooling:

```text
37,960 / 37,960 compact streams reconstructed
7,947           VS
30,009          PS
4               CS
```

The validated route is:

```text
source record
-> exact physical compact stream
-> Raw Snappy reconstruction
-> DXBC
-> D3DReflect / D3DDisassemble
-> D3D11 Create*Shader
```

The ImHex pattern is a structured human-inspection view of the PRECOMP. It is not meant to become a second independent shader extraction/decompression implementation.

## ImHex vs 010 Editor

Both editors can be used to inspect the maintained PRECOMP structure, but their template systems are different:

```text
010 Editor -> Binary Template (.bt)
ImHex      -> Pattern Language (.hexpat)
```

The `.hexpat` files here are native Pattern Language ports. ImHex does not execute the `.bt` files and does not require 010 Editor.

Likewise, the `.bt` files do not require ImHex.

For comparison during your test, use the same copied PRECOMP file in both editors and compare the main structural facts:

### DX11

```text
magic: DATA
payload version: 6
VS count
PS count
CS count
Effect count
major resolved table offsets/bounds
selected Effect -> Technique -> Pass
```

### Win32

```text
magic: SPKG
version: 2
PlatformShader count
DBUF transition
SPAR transition
SMDB boundary
```

The two editors may present the tree differently. The important question is whether they resolve the same validated binary relationships.

The two ImHex combinations above have been runtime-tested on the documented fixtures. The matching 010 Editor `.bt` files remain a separate editor-runtime verification and should not be described as covered by the ImHex test.

## Validation policy

The ImHex ports follow the same evidence policy as the maintained 010 Editor templates:

- proven structure and pointer rules are represented;
- proven shader-reference mechanics are represented;
- unknown private fields remain unknown;
- the Win32 pattern stops at its proven Version 2 boundary;
- historical names are not transplanted merely because they look plausible;
- malformed/out-of-range structures should generate visible errors rather than being silently ignored.

The exact technical validation matrix and structural boundary are documented in:

```text
../../README.md
```

The exact game build, patch date, shader corpus anchors and PRECOMP fingerprints are centralized in:

```text
../../../GAME_VERSION.md
```

## If your PRECOMP hash is different

A different SHA-256 means only that your installed PRECOMP is not byte-for-byte identical to the research fixture.

It does **not** automatically mean the structure changed.

If the pattern still reaches the expected boundaries without warnings, that is useful compatibility evidence. It is not automatically equivalent to the full research validation performed on the documented fixture.

If the pattern fails:

1. confirm you paired DX11 with the DX11 pattern or Win32 with the Win32 pattern;
2. confirm the copied file is complete;
3. check the Pattern Editor Console for the first warning/error;
4. compare the file hash with `../../../GAME_VERSION.md`;
5. preserve the failing file/version information instead of weakening the parser check.

## Common problems

### Pattern Data stays empty

Check that:

- the PRECOMP file is actually open as the active provider;
- the matching `.hexpat` source is loaded in Pattern Editor;
- you pressed **Play**;
- the Console does not report a syntax/runtime error.

Pattern Data remains empty until Pattern Language code has executed and placed patterns into the loaded data.

### Immediate magic error

Most likely causes:

- DX11 pattern used on `Shaders_Win32.precomp`;
- Win32 pattern used on `Shaders_DX11.precomp`;
- wrong/truncated file.

Expected file starts:

```text
DX11  -> DATA
Win32 -> SPKG
```

### Win32 Version warning

The maintained Win32 pattern is bounded to the observed **SPKG Version 2** file.

A different version should be investigated rather than automatically accepted as identical.

### Array/pattern limit error

The maintained patterns already raise the required evaluator limits for the validated corpus.

If you still receive a limit error, record the exact ImHex version, PRECOMP hash and Console message. Do not blindly increase limits until you know whether the input counts themselves are legitimate.

### Evaluation feels slow

Keep **Auto evaluate off**. These are large real game data sets, not small demonstration files.

### Pattern stops at SMDB on Win32

That is expected. `SMDB` is the maintained validation boundary for the documented Win32 Version 2 fixture.

## About editing values

ImHex can edit binary data, and Pattern Data can support writable values for suitable pattern types.

These PRECOMP resources are published primarily for **inspection and research**.

If you intentionally experiment with edits:

- use only a disposable copy;
- never assume changing a displayed field is safe for the game;
- remember that many relationships are validated for reading/interpretation, not as a supported authoring pipeline;
- do not overwrite the installed game file as part of a basic template test.

## Historical references

The old TS4 and TS3 `.bt` files are kept separately under:

```text
../../old-references/
```

Their source, authorship and publication dates are documented in:

```text
../../old-references/README.md
```

They are historical 010 Editor references. They have **not** been automatically converted into historical ImHex patterns.

The maintained ImHex patterns target the validation boundaries documented by this project.

## TS4 Animation Browser dependency

ImHex is not a runtime dependency of TS4 Animation Browser.

```text
TS4 Animation Browser normal use
-> ImHex is not required

manual PRECOMP inspection
-> ImHex + .hexpat is one supported research route
```

The purpose of publishing these files is to let modders and tool developers inspect the validated binary relationships themselves without needing the Browser's internal parser or purchasing 010 Editor.

## Recommended test checklist

For a clean verification, test these four combinations separately:

```text
1. Shaders_DX11.precomp
   + ImHex_TheSims4_Shaders_DX11_....hexpat
   -> runtime-tested on the fixture in GAME_VERSION.md: PASS

2. Shaders_Win32.precomp
   + ImHex_TheSims4_Shaders_Win32_....hexpat
   -> runtime-tested on the fixture in GAME_VERSION.md: PASS

3. Shaders_DX11.precomp
   + matching 010 Editor .bt

4. Shaders_Win32.precomp
   + matching 010 Editor .bt
```

For each new editor/file combination, record:

- editor version;
- PRECOMP SHA-256;
- whether the pattern/template completed;
- first warning/error if any;
- reported shader/table counts;
- whether the expected final boundary was reached.

That gives us a clean apples-to-apples comparison between the two editor implementations without relying only on appearance.
