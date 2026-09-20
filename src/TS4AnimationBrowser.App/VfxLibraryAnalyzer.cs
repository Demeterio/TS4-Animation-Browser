using System.Buffers.Binary;
using System.Text;

namespace TS4AnimationBrowser.App;

public static class VfxLibraryAnalyzer
{
    private const int MaximumNameLength = 256;
    private const int MaximumNamesPerResource = 65_536;
    private const int SearchTailBytes = 512 * 1024;
    private const int MaximumTrailingBytes = 256;
    private const uint EndMarker = 0xFFFFFFFF;

    public static IReadOnlyList<string> ReadEffectNames(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 9)
            return Array.Empty<string>();

        // Swarm name tables live near the end of the resource. The previous implementation kept
        // reparsing every suffix of a valid table while looking for the "best" candidate. On large
        // VFX tables that becomes quadratic and can leave the scanner apparently stuck on one
        // resource for a very long time. Prefer the structural marker first, then stop at the first
        // complete fallback table instead of parsing all of its suffixes again.
        var indexed = FindIndexedZStringTable(data, requireLeadingTerminator: true)
            ?? FindIndexedZStringTable(data, requireLeadingTerminator: false);
        if (indexed is not null)
            return indexed.Names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var lengthPrefixed = FindLengthPrefixedTable(data);
        return lengthPrefixed is null
            ? Array.Empty<string>()
            : lengthPrefixed.Names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static NameTableCandidate? FindIndexedZStringTable(byte[] data, bool requireLeadingTerminator)
    {
        var searchStart = Math.Max(0, data.Length - SearchTailBytes);

        if (requireLeadingTerminator)
        {
            // The documented Swarm layout places an 0xFFFFFFFF terminator immediately before the
            // effect-name handle table. Searching those four-byte markers first turns the common
            // path into a linear scan with only a handful of full parse attempts.
            for (var marker = searchStart; marker <= data.Length - 8; marker++)
            {
                if (BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(marker, 4)) != EndMarker)
                    continue;

                var start = marker + 4;
                if (!LooksLikeIndexedTableStart(data, start))
                    continue;
                if (!TryReadIndexedTable(data, start, out var names, out var endOffset))
                    continue;

                var remaining = data.Length - endOffset;
                if (remaining <= MaximumTrailingBytes)
                    return new NameTableCandidate(names, remaining);
            }

            return null;
        }

        // Fallback for versions/resources where the preceding structural marker is absent. Since
        // starts are inspected from earliest to latest, the first complete candidate is also the
        // longest candidate ending at that terminator; continuing would only parse shorter suffixes.
        for (var start = searchStart; start <= data.Length - 8; start++)
        {
            if (!LooksLikeIndexedTableStart(data, start))
                continue;
            if (!TryReadIndexedTable(data, start, out var names, out var endOffset))
                continue;

            var remaining = data.Length - endOffset;
            if (remaining <= MaximumTrailingBytes)
                return new NameTableCandidate(names, remaining);
        }

        return null;
    }

    private static NameTableCandidate? FindLengthPrefixedTable(byte[] data)
    {
        var searchStart = Math.Max(0, data.Length - SearchTailBytes);

        // Older merged Swarm resources use repeated [length][name + NUL][effect id] records and an
        // explicit "end" record. As above, return the first complete table: every later successful
        // start inside the same table would only be a shorter duplicate suffix.
        for (var start = searchStart; start <= data.Length - 12; start++)
        {
            if (!LooksLikeShortLength(data, start))
                continue;

            var offset = start;
            var names = new List<string>();
            var valid = false;

            for (var count = 0; count < MaximumNamesPerResource; count++)
            {
                if (!TryReadLengthPrefixedEntry(data, ref offset, out var name))
                    break;

                if (name.Equals("end", StringComparison.OrdinalIgnoreCase))
                {
                    valid = names.Count > 0;
                    break;
                }

                if (!IsLikelyEffectName(name))
                    break;

                names.Add(name);
            }

            if (!valid)
                continue;

            var remaining = data.Length - offset;
            if (remaining <= MaximumTrailingBytes)
                return new NameTableCandidate(names, remaining);
        }

        return null;
    }

    private static bool TryReadIndexedTable(
        byte[] data,
        int start,
        out List<string> names,
        out int endOffset)
    {
        names = new List<string>();
        endOffset = start;
        var offset = start;

        for (var count = 0; count < MaximumNamesPerResource; count++)
        {
            if (offset < 0 || offset + 4 > data.Length)
                return false;

            var compilationIndex = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
            offset += 4;

            if (compilationIndex == EndMarker)
            {
                if (names.Count == 0)
                    return false;

                endOffset = offset;
                return true;
            }

            if (!TryReadZeroTerminatedName(data, ref offset, out var name)
                || !IsLikelyEffectName(name))
            {
                return false;
            }

            names.Add(name);
        }

        return false;
    }

    private static bool TryReadLengthPrefixedEntry(byte[] data, ref int offset, out string name)
    {
        name = string.Empty;
        if (offset < 0 || offset + 8 > data.Length)
            return false;

        var length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
        if (length is < 2 or > MaximumNameLength || length > int.MaxValue)
            return false;

        var byteLength = (int)length;
        var stringStart = offset + 4;
        var idStart = stringStart + byteLength;
        if (idStart + 4 > data.Length)
            return false;

        var bytes = data.AsSpan(stringStart, byteLength);
        if (bytes[^1] != 0)
            return false;

        var textBytes = bytes[..^1];
        if (textBytes.Length == 0 || textBytes.IndexOf((byte)0) >= 0)
            return false;
        for (var index = 0; index < textBytes.Length; index++)
        {
            var value = textBytes[index];
            if (value < 0x20 || value > 0x7E)
                return false;
        }

        name = Encoding.UTF8.GetString(textBytes);
        offset = idStart + 4;
        return true;
    }

    private static bool TryReadZeroTerminatedName(byte[] data, ref int offset, out string name)
    {
        name = string.Empty;
        if (offset < 0 || offset >= data.Length)
            return false;

        var end = offset;
        var limit = Math.Min(data.Length, offset + MaximumNameLength);
        while (end < limit && data[end] != 0)
        {
            var value = data[end];
            if (value < 0x20 || value > 0x7E)
                return false;
            end++;
        }

        if (end == offset || end >= data.Length || data[end] != 0)
            return false;

        name = Encoding.UTF8.GetString(data, offset, end - offset);
        offset = end + 1;
        return true;
    }

    private static bool LooksLikeIndexedTableStart(byte[] data, int offset)
    {
        if (offset < 0 || offset + 5 > data.Length)
            return false;

        var compilationIndex = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
        if (compilationIndex == EndMarker)
            return false;

        var firstNameByte = data[offset + 4];
        return firstNameByte is >= 0x20 and <= 0x7E;
    }

    private static bool LooksLikeShortLength(byte[] data, int offset)
    {
        if (offset < 0 || offset + 4 > data.Length)
            return false;

        var length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
        return length is >= 2 and <= MaximumNameLength;
    }

    private static bool IsLikelyEffectName(string value)
    {
        if (value.Length < 2 || value.Length > MaximumNameLength - 1)
            return false;

        var hasLetter = false;
        foreach (var character in value)
        {
            if (char.IsLetter(character))
            {
                hasLetter = true;
                continue;
            }

            if (char.IsDigit(character) || character is '_' or '-' or '.')
                continue;

            return false;
        }

        return hasLetter;
    }

    private sealed record NameTableCandidate(
        IReadOnlyList<string> Names,
        int RemainingBytes);
}
