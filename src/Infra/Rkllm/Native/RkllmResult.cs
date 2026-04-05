using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmResult {
    public IntPtr Text;
    public int TokenId;
    public RkllmResultLastHiddenLayer LastHiddenLayer;
    public RkllmResultLogits Logits;
    public RkllmPerfStat Performance;
}
