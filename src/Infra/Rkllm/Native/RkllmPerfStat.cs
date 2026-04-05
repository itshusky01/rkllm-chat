using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmPerfStat {
    public float PrefillTimeMs;
    public int PrefillTokens;
    public float GenerateTimeMs;
    public int GenerateTokens;
    public float MemoryUsageMb;
}
