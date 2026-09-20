namespace TS4AnimationBrowser.Core.Dbpf;

public sealed record ResourceEntry(
    ResourceKey Key,
    uint Offset,
    uint CompressedSize,
    uint UncompressedSize,
    DbpfCompressionType Compression,
    ushort Committed,
    bool HasExtendedMetadata)
{
    public string TypeHex => $"0x{Key.Type:X8}";
    public string GroupHex => $"0x{Key.Group:X8}";
    public string InstanceHex => $"0x{Key.Instance:X16}";
    public string TypeName => KnownResourceTypes.GetName(Key.Type);
}
