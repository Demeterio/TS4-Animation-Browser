using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TS4AnimationBrowser.Core.Dbpf;

namespace TS4AnimationBrowser.App;

public sealed record DtabCacheResult(
    IReadOnlyList<ResourceRow> Resources,
    string GameRoot,
    int PackageCount,
    int ErrorCount,
    int ShadowedResourceCount,
    DateTime CreatedUtc);

public sealed record CacheSaveProgress(string Stage, int Percent, string Detail);

public static class DtabCache
{
    private const uint FileMagic = 0x42415444; // DTAB
    private const ushort ContainerVersion = 2;
    private const int PayloadVersion = 2;
    private const int MaximumProtectedSize = 256 * 1024 * 1024;
    private const int MaximumResourceCount = 2_000_000;
    private const int IoBlockSize = 256 * 1024;
    private const string CacheFileName = "TS4AnimationBrowser.dtab";

    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("TS4 Animation Browser DTAB CurrentUser v2");

    public static event Action<CacheSaveProgress>? SaveProgressChanged;

    public static string CachePath => Path.Combine(AppContext.BaseDirectory, CacheFileName);

    public static DtabCacheResult? TryLoad()
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            byte[] protectedBytes;
            using (var stream = new FileStream(CachePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false))
            {
                if (stream.Length < sizeof(uint) + sizeof(ushort) + sizeof(int))
                    return null;
                if (reader.ReadUInt32() != FileMagic || reader.ReadUInt16() != ContainerVersion)
                    return null;

                var protectedLength = reader.ReadInt32();
                if (protectedLength <= 0
                    || protectedLength > MaximumProtectedSize
                    || protectedLength > stream.Length - stream.Position)
                    return null;

                protectedBytes = reader.ReadBytes(protectedLength);
                if (protectedBytes.Length != protectedLength)
                    return null;
            }

            var compressedPayload = ProtectedData.Unprotect(
                protectedBytes,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            var payload = Decompress(compressedPayload);
            return DeserializePayload(payload);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static bool TrySave(string gameRoot, ScanResult result)
    {
        var temporaryPath = CachePath + ".tmp";
        try
        {
            ReportSaveProgress("Serializing library…", 0, $"Preparing {result.Resources.Count:N0} indexed resources…");
            var payload = SerializePayload(gameRoot, result);

            ReportSaveProgress("Compressing cache…", 45, $"Compressing {payload.Length:N0} bytes…");
            var compressedPayload = Compress(payload);

            ReportSaveProgress("Encrypting cache…", 75, $"Protecting {compressedPayload.Length:N0} compressed bytes with Windows DPAPI…");
            var protectedBytes = ProtectedData.Protect(
                compressedPayload,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            if (protectedBytes.Length > MaximumProtectedSize)
                return false;

            ReportSaveProgress("Writing cache…", 90, $"Writing {protectedBytes.Length:N0} encrypted bytes to disk…");
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(FileMagic);
                writer.Write(ContainerVersion);
                writer.Write(protectedBytes.Length);
                writer.Flush();

                var written = 0;
                while (written < protectedBytes.Length)
                {
                    var count = Math.Min(IoBlockSize, protectedBytes.Length - written);
                    stream.Write(protectedBytes, written, count);
                    written += count;
                    ReportMappedProgress(
                        "Writing cache…",
                        written,
                        protectedBytes.Length,
                        90,
                        99,
                        $"Writing encrypted cache • {written:N0} / {protectedBytes.Length:N0} bytes");
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, CachePath, overwrite: true);
            ReportSaveProgress("Cache saved", 100, $"Encrypted library cache saved for {AppInfo.Version}.");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static byte[] SerializePayload(string gameRoot, ScanResult result)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(PayloadVersion);
        writer.Write(AppInfo.Version);
        writer.Write(gameRoot);
        writer.Write(DateTime.UtcNow.Ticks);
        writer.Write(result.PackageCount);
        writer.Write(result.ErrorCount);
        writer.Write(result.ShadowedResourceCount);
        writer.Write(result.Resources.Count);

        var resourceCount = result.Resources.Count;
        var lastReportedPercent = -1;
        for (var index = 0; index < resourceCount; index++)
        {
            var row = result.Resources[index];
            var entry = row.Entry;
            writer.Write(entry.Key.Type);
            writer.Write(entry.Key.Group);
            writer.Write(entry.Key.Instance);
            writer.Write(entry.Offset);
            writer.Write(entry.CompressedSize);
            writer.Write(entry.UncompressedSize);
            writer.Write((ushort)entry.Compression);
            writer.Write(entry.Committed);
            writer.Write(entry.HasExtendedMetadata);
            writer.Write(row.PackagePath);
            writer.Write(row.Name ?? string.Empty);
            writer.Write(row.IsRelevant);
            writer.Write((byte)row.Category);

            var current = index + 1;
            var percent = resourceCount == 0
                ? 45
                : (int)Math.Floor(current * 45.0 / resourceCount);
            if (percent != lastReportedPercent)
            {
                lastReportedPercent = percent;
                ReportSaveProgress(
                    "Serializing library…",
                    percent,
                    $"Resource {current:N0} / {resourceCount:N0}");
            }
        }

        writer.Flush();
        if (resourceCount == 0)
            ReportSaveProgress("Serializing library…", 45, "Library metadata serialized.");
        return stream.ToArray();
    }

    private static DtabCacheResult? DeserializePayload(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        if (reader.ReadInt32() != PayloadVersion)
            return null;

        var browserVersion = reader.ReadString();
        if (!string.Equals(browserVersion, AppInfo.Version, StringComparison.Ordinal))
            return null;

        var gameRoot = reader.ReadString();
        var createdUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
        var packageCount = reader.ReadInt32();
        var errorCount = reader.ReadInt32();
        var shadowedResourceCount = reader.ReadInt32();
        var resourceCount = reader.ReadInt32();

        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            return null;
        if (packageCount < 0 || errorCount < 0 || shadowedResourceCount < 0)
            return null;
        if (resourceCount < 0 || resourceCount > MaximumResourceCount)
            return null;

        var rows = new List<ResourceRow>(resourceCount);
        for (var index = 0; index < resourceCount; index++)
        {
            var type = reader.ReadUInt32();
            var group = reader.ReadUInt32();
            var instance = reader.ReadUInt64();
            var offset = reader.ReadUInt32();
            var compressedSize = reader.ReadUInt32();
            var uncompressedSize = reader.ReadUInt32();
            var compression = (DbpfCompressionType)reader.ReadUInt16();
            var committed = reader.ReadUInt16();
            var hasExtendedMetadata = reader.ReadBoolean();
            var packagePath = reader.ReadString();
            var name = reader.ReadString();
            var isRelevant = reader.ReadBoolean();
            var categoryValue = reader.ReadByte();

            if (!Enum.IsDefined(typeof(ResourceVisualCategory), (int)categoryValue))
                return null;

            var entry = new ResourceEntry(
                new ResourceKey(type, group, instance),
                offset,
                compressedSize,
                uncompressedSize,
                compression,
                committed,
                hasExtendedMetadata);
            var row = new ResourceRow(entry, packagePath, gameRoot, name, isRelevant);
            row.SetCategory((ResourceVisualCategory)categoryValue);
            rows.Add(row);
        }

        if (stream.Position != stream.Length)
            return null;

        return new DtabCacheResult(
            rows,
            gameRoot,
            packageCount,
            errorCount,
            shadowedResourceCount,
            createdUtc);
    }

    private static byte[] Compress(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var processed = 0;
            while (processed < payload.Length)
            {
                var count = Math.Min(IoBlockSize, payload.Length - processed);
                brotli.Write(payload, processed, count);
                processed += count;
                ReportMappedProgress(
                    "Compressing cache…",
                    processed,
                    payload.Length,
                    45,
                    75,
                    $"Compressing cache • {processed:N0} / {payload.Length:N0} bytes");
            }
        }

        if (payload.Length == 0)
            ReportSaveProgress("Compressing cache…", 75, "Cache compression complete.");
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] compressedPayload)
    {
        using var input = new MemoryStream(compressedPayload, writable: false);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    private static void ReportMappedProgress(
        string stage,
        int current,
        int total,
        int startPercent,
        int endPercent,
        string detail)
    {
        var ratio = total <= 0 ? 1.0 : Math.Clamp(current / (double)total, 0.0, 1.0);
        var percent = startPercent + (int)Math.Round((endPercent - startPercent) * ratio);
        ReportSaveProgress(stage, percent, detail);
    }

    private static void ReportSaveProgress(string stage, int percent, string detail)
    {
        var progress = new CacheSaveProgress(stage, Math.Clamp(percent, 0, 100), detail);
        try
        {
            SaveProgressChanged?.Invoke(progress);
        }
        catch
        {
            // Progress reporting must never make cache persistence fail.
        }
    }
}
