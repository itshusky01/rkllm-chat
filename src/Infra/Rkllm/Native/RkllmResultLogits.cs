using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmResultLogits {
    public IntPtr Logits;
    public int VocabularySize;
    public int TokenCount;
}
