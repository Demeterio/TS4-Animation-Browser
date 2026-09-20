namespace TS4AnimationBrowser.Core.Dbpf;

public enum DbpfCompressionType : ushort
{
    None = 0x0000,
    Zlib = 0x5A42,
    Deleted = 0xFFE0,
    Streamable = 0xFFFE,
    Internal = 0xFFFF
}
