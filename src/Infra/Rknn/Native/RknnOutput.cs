using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rknn.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RknnOutput {
    public byte WantFloat;
    public byte IsPreAllocated;
    public uint Index;
    public IntPtr Buffer;
    public uint Size;
}
