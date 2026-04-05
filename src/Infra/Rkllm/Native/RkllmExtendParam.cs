using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmExtendParam {
    public int BaseDomainId;
    public sbyte EmbedFlash;
    public sbyte EnabledCpuCount;
    public uint EnabledCpuMask;
    public byte BatchSize;
    public sbyte UseCrossAttention;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 104)]
    public byte[] Reserved;
}
