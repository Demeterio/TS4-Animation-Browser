namespace TS4AnimationBrowser.Core.Dbpf;

public readonly record struct ResourceKey(uint Type, uint Group, ulong Instance)
{
    public override string ToString() => $"{Type:X8}:{Group:X8}:{Instance:X16}";
}
