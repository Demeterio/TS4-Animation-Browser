#pragma author Demeterio
#pragma description "The Sims 4 Shaders_DX11.precomp - validated DATA/shaders v6 view"
#pragma endian little
#pragma magic [ 44 41 54 41 ] @ 0x00

// ImHex evaluator limits, not PRECOMP format limits. The default ImHex limits
// are lower than the validated TS4 shader-table populations (30,009 PS alone).
#pragma array_limit 0x10000000
#pragma pattern_limit 0x1000000

import std.io;

// Validated fixture: The Sims 4 PC 1.126.73.1030.
// Publication boundary:
// DATA -> shaders payload v6 -> effect-like outer record -> Technique -> Pass
// -> 1-based VS/PS/CS source reference -> source record
// -> exact Raw Snappy stream -> DXBC.
//
// Naming boundary:
// - Dx11EffectRecord / Effect is a maintained structural label for the proven
//   current outer record; it is not asserted as the stripped current EA type.
// - Technique / Pass describe the independently proven current hierarchy and
//   also occur in preserved first-party historical PRECOMP vocabulary.
// - VSRef / PSRef / CSRef / StateStart / StateCount are descriptive current
//   dataflow labels, not claims about stripped private member names.
// - ShaderPreparation* is structural/descriptive; internal fields remain raw.
//
// Unknown values intentionally remain unknown. Raw Snappy decompression is not
// implemented by this inspection pattern.

#define SELECT_EFFECT 0
#define SELECT_TECHNIQUE 0

fn valid_range(auto address, auto size) {
    return address >= 0 && address + size <= sizeof($);
};

struct DataDescriptor {
    u32 Word0;
    u32 Word1;
    u32 Word2;
    u32 Word3;
    u32 Word4;
    u32 Word5; // observed absolute file-region start in current descriptors
    u32 Word6;
} [[static]];

struct DataRootEntry {
    s32 NameRelative;
    u32 Unknown04;
    u32 Unknown08;
    u32 Unknown0C;
    u32 Unknown10;
    s32 PayloadRelative;
} [[static]];

struct Dx11EffectRecord {
    u32 Field00; // strongly correlated with known MATD Shader hashes; not renamed
    u32 Unknown04;
    u32 Unknown08;
    u32 Unknown0C;
    u32 Unknown10;
    s32 TechniquesRelative;
    u32 TechniqueCount;
} [[static]]; // maintained structural name; current EA private type name unknown

struct Dx11Technique {
    u32 Unknown00;
    s32 PassesRelative;
    u32 PassCount;
} [[static]];

struct Dx11Pass {
    u32 VSRef;            // 1-based, 0 = absent; current dataflow role proven
    u32 PSRef;            // 1-based, 0 = absent; current dataflow role proven
    u32 CSRef;            // 1-based, 0 = absent; current dataflow role proven
    u32 StateStart;       // current state-slice role proven; private name unknown
    u32 StateCount;       // current state-slice role proven; private name unknown
    s32 OptionalRelative; // INT_MIN = absent; target semantics unknown
} [[static]];

struct Dx11StatePair {
    u32 StateId; // descriptive pair-word label; exact private business name unknown
    u32 Value;   // descriptive pair-value label; exact private business name unknown
} [[static]];

struct Dx11VertexShaderSourceRecord {
    u8 Raw[0x34];
} [[static]];

struct Dx11PixelShaderSourceRecord {
    u8 Raw[0x2C];
} [[static]];

struct Dx11ComputeShaderSourceRecord {
    u8 Raw[0x2C];
} [[static]];

struct Dx11ShaderPreparationRecord {
    u8 Raw[0x1C];
} [[static]]; // structural/descriptive name; internal fields remain unknown

struct Dx11PayloadHeader {
    u32 PayloadVersion;
    s32 EffectsRelative; // maintained structural label: effect-like outer record table
    u32 EffectCount;     // count of the proven outer record table
    u32 PayloadUnknown0C;
    u32 PayloadUnknown10;
    s32 VSRelative;
    u32 VSCount;
    s32 PSRelative;
    u32 PSCount;
    s32 CSRelative;
    u32 CSCount;
    u32 PayloadUnknown2C;
    u32 PayloadUnknown30;
    u32 PayloadUnknown34;
    u32 PayloadUnknown38;
    u32 PayloadUnknown3C;
    u32 PayloadUnknown40;
    u32 PayloadUnknown44;
    u32 PayloadUnknown48;
    s32 StatePairsRelative;
    u32 StatePairBoundRaw;
    s32 ShaderPreparationRelative;
    u32 ShaderPreparationCount;
    u32 PayloadUnknown5C;
    u32 PayloadUnknown60;
} [[static]];

struct Dx11Precomp {
    char Magic[4];
    u32 DataUnknown04;
    s32 RootEntryRelative;
    u32 DescriptorCount;
    u32 DataUnknown10;
    u32 DataUnknown14;
    u32 DataUnknown18;
    u32 DataUnknown1C;

    DataDescriptor Descriptors[DescriptorCount];

    s64 rootEntryOffset = addressof(RootEntryRelative) + RootEntryRelative;
    if (!valid_range(rootEntryOffset, 0x18)) {
        std::error("DATA root-entry relative pointer is outside the file");
    }

    DataRootEntry RootEntry @ rootEntryOffset;

    s64 payloadOffset = addressof(RootEntry.PayloadRelative) + RootEntry.PayloadRelative;
    if (!valid_range(payloadOffset, 0x64)) {
        std::error("shaders payload relative pointer is outside the file");
    }

    Dx11PayloadHeader Payload @ payloadOffset;

    if (Payload.PayloadVersion != 6) {
        std::warning("Observed EA DX11 loader expects shaders payload Version 6");
    }

    s64 effectBase = addressof(Payload.EffectsRelative) + Payload.EffectsRelative;
    s64 vsBase = addressof(Payload.VSRelative) + Payload.VSRelative;
    s64 psBase = addressof(Payload.PSRelative) + Payload.PSRelative;
    s64 csBase = addressof(Payload.CSRelative) + Payload.CSRelative;
    s64 stateBase = addressof(Payload.StatePairsRelative) + Payload.StatePairsRelative;
    s64 shaderPreparationBase = addressof(Payload.ShaderPreparationRelative) + Payload.ShaderPreparationRelative;

    if (Payload.EffectCount > 0 && valid_range(effectBase, Payload.EffectCount * 0x1C)) {
        Dx11EffectRecord Effects[Payload.EffectCount] @ effectBase;
    } else if (Payload.EffectCount > 0) {
        std::warning("Effect-like outer record table is outside the file");
    }

    if (Payload.VSCount > 0 && valid_range(vsBase, Payload.VSCount * 0x34)) {
        Dx11VertexShaderSourceRecord VertexShaderSourceRecords[Payload.VSCount] @ vsBase;
    } else if (Payload.VSCount > 0) {
        std::warning("Vertex-shader source table is outside the file");
    }

    if (Payload.PSCount > 0 && valid_range(psBase, Payload.PSCount * 0x2C)) {
        Dx11PixelShaderSourceRecord PixelShaderSourceRecords[Payload.PSCount] @ psBase;
    } else if (Payload.PSCount > 0) {
        std::warning("Pixel-shader source table is outside the file");
    }

    if (Payload.CSCount > 0 && valid_range(csBase, Payload.CSCount * 0x2C)) {
        Dx11ComputeShaderSourceRecord ComputeShaderSourceRecords[Payload.CSCount] @ csBase;
    } else if (Payload.CSCount > 0) {
        std::warning("Compute-shader source table is outside the file");
    }

    if (Payload.ShaderPreparationCount > 0 && valid_range(shaderPreparationBase, Payload.ShaderPreparationCount * 0x1C)) {
        Dx11ShaderPreparationRecord ShaderPreparationRecords[Payload.ShaderPreparationCount] @ shaderPreparationBase;
    } else if (Payload.ShaderPreparationCount > 0) {
        std::warning("Shader-preparation table is outside the file");
    }

    // Payload +0x50 is proven to bound every referenced state slice in the
    // validated corpus. Its formal private/business semantic remains unknown.
    if (Payload.StatePairBoundRaw > 0 && valid_range(stateBase, Payload.StatePairBoundRaw * 0x08)) {
        Dx11StatePair StatePairs[Payload.StatePairBoundRaw] @ stateBase;
    } else if (Payload.StatePairBoundRaw > 0) {
        std::warning("State-pair table is outside the file");
    }

    // Convenience selected outer-record -> Technique -> Pass view, mirroring
    // the maintained 010 Editor template without changing the file.
    if (SELECT_EFFECT >= 0 && SELECT_EFFECT < Payload.EffectCount && valid_range(effectBase + SELECT_EFFECT * 0x1C, 0x1C)) {
        Dx11EffectRecord SelectedEffect @ (effectBase + SELECT_EFFECT * 0x1C);
        s64 selectedTechniqueBase = addressof(SelectedEffect.TechniquesRelative) + SelectedEffect.TechniquesRelative;

        if (SelectedEffect.TechniqueCount > 0 && valid_range(selectedTechniqueBase, SelectedEffect.TechniqueCount * 0x0C)) {
            Dx11Technique SelectedEffectTechniques[SelectedEffect.TechniqueCount] @ selectedTechniqueBase;

            if (SELECT_TECHNIQUE >= 0 && SELECT_TECHNIQUE < SelectedEffect.TechniqueCount) {
                Dx11Technique SelectedTechnique @ (selectedTechniqueBase + SELECT_TECHNIQUE * 0x0C);
                s64 selectedPassBase = addressof(SelectedTechnique.PassesRelative) + SelectedTechnique.PassesRelative;

                if (SelectedTechnique.PassCount > 0 && valid_range(selectedPassBase, SelectedTechnique.PassCount * 0x18)) {
                    Dx11Pass SelectedPasses[SelectedTechnique.PassCount] @ selectedPassBase;
                } else if (SelectedTechnique.PassCount > 0) {
                    std::warning("Selected pass table is outside the file");
                }
            }
        }
    }
};

Dx11Precomp Precomp @ 0x00;

// DATAFLOW / STRUCTURE PROVEN:
//   VSRef N -> stage VS record [N - 1]
//   PSRef N -> stage PS record [N - 1]
//   CSRef N -> stage CS record [N - 1]
//
// Validated compact-stream global ordering:
//   0..7946      = VS
//   7947..37955  = PS
//   37956..37959 = CS
//
// Source-record +0x00 resolves to the exact physical Raw Snappy stream for:
//   VS 7,947 / 7,947
//   PS 30,009 / 30,009
//   CS 4 / 4
//
// Compact streams are intentionally not decompressed by this inspection pattern.
