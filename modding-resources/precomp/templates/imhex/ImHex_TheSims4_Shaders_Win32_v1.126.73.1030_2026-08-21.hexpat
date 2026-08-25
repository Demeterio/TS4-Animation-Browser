#pragma author Demeterio
#pragma description "The Sims 4 Shaders_Win32.precomp - validated SPKG Version 2 prefix"
#pragma endian little
#pragma magic [ 53 50 4B 47 ] @ 0x00

// ImHex evaluator limits, not PRECOMP format limits. The validated file has
// 27,894 PlatformShader records, above ImHex's default array/pattern limits.
#pragma array_limit 0x10000000
#pragma pattern_limit 0x1000000

import std.io;

// Validated fixture: The Sims 4 PC 1.126.73.1030.
// Publication boundary:
// SPKG -> SHDB -> LINK -> PlatformShader[] -> DBUF -> SPAR -> SMDB.
//
// Naming boundary:
// - PlatformShader / IsPixelShader / ShaderPlatformMask / ShaderSize /
//   ByteCodeSize / ByteCode occur in preserved first-party Version 1 material,
//   while the Version 2 layout/mechanics shown here are independently validated.
// - Current SPAR records are proven 0x08-byte records, but older member
//   semantics are not transferred: Word0/Word1 stay unknown.
// - Historical TECH/PASS/PARM names below SMDB remain historical evidence only.
//
// The historical Version 1 TECH/PASS/PARM tail is intentionally not promoted
// into this Version 2 pattern without independent mechanical validation.

struct PlatformShader {
    u8 IsPixelShader;
    u16 ShaderPlatformMask;
    u32 ShaderSize;
    char ShaderTag[4];
    u32 ByteCodeSize;

    if (ByteCodeSize > sizeof($) - $) {
        std::error("PlatformShader bytecode extends beyond the file");
    }

    u8 ByteCode[ByteCodeSize];
};

struct ShaderParameterRecord {
    u32 Word0; // current SPAR record semantics intentionally not renamed
    u32 Word1; // current SPAR record semantics intentionally not renamed
} [[static]]; // maintained structural name for the current 0x08-byte SPAR record

struct Win32PrecompPrefix {
    char HeaderTag[4];
    u32 Version;

    if (Version != 2) {
        std::warning("This pattern is bounded to the observed SPKG Version 2 file");
    }

    char ShaderDbTag[4];
    if (ShaderDbTag[0] != 'S' || ShaderDbTag[1] != 'H' || ShaderDbTag[2] != 'D' || ShaderDbTag[3] != 'B') {
        std::error("Expected SHDB after the SPKG header");
    }

    u32 ShaderDbSize;

    char LinkTag[4];
    if (LinkTag[0] != 'L' || LinkTag[1] != 'I' || LinkTag[2] != 'N' || LinkTag[3] != 'K') {
        std::error("Expected LINK in the shader database prefix");
    }

    u32 PlatformShaderCount;

    // Each PlatformShader has at least 15 bytes before any bytecode payload.
    // This is a parser bounds check, not a claimed semantic maximum count.
    if (PlatformShaderCount > (sizeof($) - $) / 15) {
        std::error("PlatformShader count cannot fit in the remaining file");
    }

    PlatformShader PlatformShaders[PlatformShaderCount];

    char DefaultBufferTag[4];
    if (DefaultBufferTag[0] != 'D' || DefaultBufferTag[1] != 'B' || DefaultBufferTag[2] != 'U' || DefaultBufferTag[3] != 'F') {
        std::error("Expected DBUF after PlatformShader[]");
    }

    u32 DefaultsSize;
    if (DefaultsSize > (sizeof($) - $) / 4) {
        std::error("DBUF data extends beyond the file");
    }
    u32 DefaultData[DefaultsSize];

    char ShaderParameterTag[4];
    if (ShaderParameterTag[0] != 'S' || ShaderParameterTag[1] != 'P' || ShaderParameterTag[2] != 'A' || ShaderParameterTag[3] != 'R') {
        std::error("Expected SPAR after DBUF");
    }

    u32 ShaderParameterCount;
    if (ShaderParameterCount > (sizeof($) - $) / 8) {
        std::error("SPAR record table extends beyond the file");
    }
    ShaderParameterRecord ShaderParameters[ShaderParameterCount];

    char ShaderModelDatabaseTag[4];
    if (ShaderModelDatabaseTag[0] != 'S' || ShaderModelDatabaseTag[1] != 'M' || ShaderModelDatabaseTag[2] != 'D' || ShaderModelDatabaseTag[3] != 'B') {
        std::warning("Current validated prefix expected SMDB after SPAR");
    }
};

Win32PrecompPrefix Prefix @ 0x00;

// Observed population in the validated file:
//   7,521 VS
//   20,373 PS
//   27,894 total PlatformShader records
//
// Current validation deliberately stops after the SMDB tag. Historical TS4
// Version 1 structures such as SETS/SSET, SHDS/SBLK, TECH, PASS, PARM, KNM and
// END remain reference material only until Version 2 is independently proven.
