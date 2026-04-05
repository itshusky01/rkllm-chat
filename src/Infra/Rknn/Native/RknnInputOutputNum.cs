using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rknn.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RknnInputOutputNum {
    public uint InputCount;
    public uint OutputCount;
}
