using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmParam {
    public IntPtr ModelPath;
    public int MaxContextLen;
    public int MaxNewTokens;
    public int TopK;
    public int TokensToKeep;
    public float TopP;
    public float Temperature;
    public float RepeatPenalty;
    public float FrequencyPenalty;
    public float PresencePenalty;
    public int Mirostat;
    public float MirostatTau;
    public float MirostatEta;

    [MarshalAs(UnmanagedType.I1)]
    public bool SkipSpecialToken;

    [MarshalAs(UnmanagedType.I1)]
    public bool IsAsync;

    public IntPtr ImageStart;
    public IntPtr ImageEnd;
    public IntPtr ImageContent;
    public RkllmExtendParam ExtendParam;
}
