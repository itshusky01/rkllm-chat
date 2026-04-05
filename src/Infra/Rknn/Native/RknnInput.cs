using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rknn.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RknnInput {
    public uint Index;
    public IntPtr Buffer;
    public uint Size;
    public byte PassThrough;
    public RknnTensorType Type;
    public RknnTensorFormat Format;
}
