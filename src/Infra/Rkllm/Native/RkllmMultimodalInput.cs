using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct RkllmMultimodalInput {
    public IntPtr Prompt;
    public float* ImageEmbedding;
    public nuint ImageTokenCount;
    public nuint ImageCount;
    public nuint ImageWidth;
    public nuint ImageHeight;
}
