using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmResultLastHiddenLayer {
    public IntPtr HiddenStates;
    public int EmbeddingSize;
    public int TokenCount;
}
