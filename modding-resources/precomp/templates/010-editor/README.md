# 010 Editor — The Sims 4 PRECOMP templates

This folder contains the maintained **010 Editor Binary Templates** for the validated The Sims 4 PRECOMP formats.

If shaders, PRECOMP or Binary Templates are new to you, start with:

```text
../../../BEGINNER_GUIDE.md
```

For the exact validated game build, patch date and PRECOMP fingerprints, use:

```text
../../../GAME_VERSION.md
```

Historical TS3/TS4 templates, their authors and their original sources are documented separately in:

```text
../../old-references/README.md
```

## Templates

### DX11

```text
010Editor_TheSims4_Shaders_DX11_vX_YYYY-MM-DD.precomp.bt
```

Use it with a copy of:

```text
The Sims 4\Game\Bin\res\Shaders_DX11.precomp
```

It follows the validated modern route beginning with `DATA` and the `shaders` payload Version 6.

### Win32 Version 2

```text
010Editor_TheSims4_Shaders_Win32_vX_YYYY-MM-DD.precomp.bt
```

Use it with a copy of:

```text
The Sims 4\Game\Bin\res\Shaders_Win32.precomp
```

It intentionally parses only the mechanically revalidated prefix for the documented fixture:

```text
SPKG -> SHDB -> LINK -> PlatformShader[] -> DBUF -> SPAR -> SMDB
```

It does not copy the historical Version 1 `TECH/PASS/PARM` tail without independent proof that the Version 2 layout is unchanged.

## Runtime validation in 010 Editor

The maintained `.bt` files were manually executed in **010 Editor** against the fixture identified in `../../../GAME_VERSION.md`.

### DX11 runtime result

The DX11 template ran against the documented fixture and produced the expected top-level `DATA` / `shaders` payload structure.

The observed values included:

```text
Magic          = DATA
RootName       = shaders
PayloadVersion = 6
EffectCount    = 77,771
VSCount        = 7,947
PSCount        = 30,009
CSCount        = 4
```

These values match the independent ImHex runtime result and the dedicated PRECOMP research tooling.

The selected navigation helper was also expanded and recorded in 010 Editor. It resolved:

```text
SelectedEffect             @ 0x001A7D30
  TechniqueCount           = 6
  TechniquesRelative       = 2,177,580

SelectedEffectTechniques   @ 0x003BB770
SelectedTechnique          @ 0x003BB770
  PassCount                = 1
  PassesRelative           = 2,524,268

SelectedPasses             @ 0x00623BE0
  VSRef                    = 5,880
  PSRef                    = 25,452
  CSRef                    = 0
  StateStart               = 103,303
  StateCount               = 1
```

The addresses reproduce the maintained field-relative pointer rule:

```text
target = address_of(relative_field) + relative_value
```

The selected pass shader references are in range for the validated stage tables, and `CSRef = 0` represents an absent compute stage. This independently matches the ImHex runtime result for the same fixture.

The maintained 010 Editor DX11 template is therefore runtime-confirmed for both the core structure/count parsing and the selected `Effect -> Technique -> Pass` convenience view.

### Win32 Version 2 runtime result

The Win32 template was also run against the exact documented fixture.

The first run exposed a **010 Editor template-engine issue**, not an EA-format contradiction: `PlatformShader` is variable-sized because `ByteCodeSize` controls `ByteCode[]`, but 010 Editor attempted to optimize the `PlatformShaders[]` array as though every element had the same size. That caused the parser to seek `DBUF` at the wrong offset.

The maintained template was corrected to disable that invalid optimization explicitly:

```c
PlatformShader PlatformShaders[PlatformShaderCount] <optimize=false>;
```

After that correction, the template was rerun successfully and reached the complete maintained boundary:

```text
SPKG
-> SHDB
-> LINK
-> PlatformShader[]
-> DBUF
-> SPAR
-> SMDB
```

The observed fixture values/offsets included:

```text
Version                  = 2
PlatformShaderCount      = 27,894
DBUF                     @ 0x02799D6A
DefaultsSize             = 1,994,786
SPAR                     @ 0x02F35DFA
ShaderParameterCount     = 661
SMDB                     @ 0x02F372AA
```

This confirms that the corrected 010 Editor Win32 template reproduces the same published Version 2 prefix already validated independently in ImHex.

The explicit `<optimize=false>` is therefore intentional and must not be removed merely to make the template look simpler or to re-enable 010 Editor's array optimization. The records are genuinely variable-sized.

These runtime checks apply to the fixture identified in `../../../GAME_VERSION.md`. They do not automatically extend the claim to later game builds with different PRECOMP hashes, and they do not turn still-unknown private fields into known semantics.

## Safe quick start

Do not experiment on the only copy of a PRECOMP inside the game installation.

1. Copy the matching `.precomp` file to a working folder.
2. Start **010 Editor**.
3. Use **File -> Open File...** and open that PRECOMP copy.
4. Open the matching `.bt` Binary Template from this folder.
5. Use 010 Editor's **Run Template** command/button.
6. Expand the resulting structures in the Template Results/Variables view.
7. Select fields to correlate the structured values with their bytes in the hex view.

Running a Binary Template is an inspection/parsing operation. It does not automatically rewrite the PRECOMP.

## DX11 selected effect/technique view

Near the top of the DX11 template are:

```c
local int SELECT_EFFECT = 0;
local int SELECT_TECHNIQUE = 0;
```

These values control an additional convenience view that follows the validated relative-pointer rules for one effect/technique.

Set:

```c
local int SELECT_EFFECT = -1;
```

if you do not want that additional selected view.

Changing those template variables does not modify the PRECOMP file.

## What the DX11 template intentionally does not do

The compact DX11 shader streams were proven to be Raw Snappy data that reconstructs to DXBC, but the `.bt` deliberately does not contain another Snappy decoder.

The complete research validation used dedicated tooling to reconstruct and validate all 37,960 shaders. Keeping that logic outside the display template avoids creating a second independent decompressor that could diverge from the tested parser.

Fields shown as `Unknown`, `WordN` or raw byte arrays are intentional. A known offset or physical role is not silently turned into an unsupported private EA field name.

## Warnings are useful

The maintained templates include signature/version/bounds checks.

If a future game update triggers one, treat the warning as evidence that the new file should be compared with the validated research build. Do not simply weaken the check until the changed bytes are understood.

A template successfully parsing a later file is useful compatibility evidence, but it is not by itself equivalent to the full validation documented in `../../README.md` and `../../../GAME_VERSION.md`.

The Win32 runtime test also demonstrated a second kind of warning that matters: an editor/template-engine optimization warning can reveal incorrect navigation even when the EA file itself is valid. The maintained Win32 array therefore keeps `<optimize=false>` because `PlatformShader` records have variable bytecode lengths.

## What is 010 Editor?

010 Editor is a commercial hexadecimal/binary editor by SweetScape. Its **Binary Template** system lets a `.bt` file describe structures, arrays, values and relationships in a binary file so they can be explored as structured data instead of only raw hexadecimal bytes.

The maintained files in this folder use ordinary Binary Template constructs; they intentionally do not claim that a specific modern major version is the only version capable of running them.

At the time this resource set was prepared, SweetScape's current public release was:

```text
010 Editor 16.0.4
```

Official product page:

https://www.sweetscape.com/010editor/

## Trial and licensing

010 Editor is **not** a permanently free edition with reduced functionality. SweetScape currently offers a **30-day free trial**; a license is required to continue using it after the trial.

Official download page:

https://www.sweetscape.com/download/

At the time this documentation was prepared, SweetScape listed new-user prices as:

```text
Home/Academic use:  $59.95 US
Commercial use:    $149.95 US
```

Official store:

https://www.sweetscape.com/store/

The distinction is primarily the permitted type of use rather than a separate crippled/full application build. Consult SweetScape's current license terms if the exact legal category matters to your use case.

SweetScape currently describes purchased licenses as **perpetual**. New purchases include one year of free upgrades/support; maintenance/upgrades after that period are separate, while the licensed version can continue to be used.

## Do I need to buy 010 Editor to use TS4 Animation Browser?

No.

```text
TS4 Animation Browser normal use
-> 010 Editor is not required

manual PRECOMP inspection with these .bt files
-> 010 Editor is the intended editor
```

## Free/open-source alternative

Native **ImHex Pattern Language** ports are included in the sibling folder:

```text
../imhex/
```

ImHex is free/open-source, but its `.hexpat` Pattern Language is different from 010 Editor's `.bt` language. The files in the ImHex folder are therefore maintained ports of the same validation boundary, not files that ImHex executes directly from these `.bt` templates.

Both maintained ImHex patterns have also been runtime-tested against the same documented PRECOMP fixtures. That independent editor implementation is useful as a cross-check when validating the same physical binary boundaries.

## Validation and historical references

For the exact semantic/structural boundary and shader-package details, read:

```text
../../README.md
```

For the exact game build, patch date and PRECOMP fingerprints, read:

```text
../../../GAME_VERSION.md
```

For old TS4/TS3 provenance and authorship:

```text
../../old-references/README.md
```

For a technical old-vs-current comparison:

```text
../../guides/LEGACY_TEMPLATES.md
```
