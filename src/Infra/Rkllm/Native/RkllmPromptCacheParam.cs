using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmPromptCacheParam {
    public int SavePromptCache;
    public IntPtr PromptCachePath;
}
