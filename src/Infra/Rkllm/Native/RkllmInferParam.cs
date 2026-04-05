using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmInferParam {
    public RkllmInferMode Mode;
    public IntPtr LoraParameters;
    public IntPtr PromptCacheParameters;
    public int KeepHistory;
}
