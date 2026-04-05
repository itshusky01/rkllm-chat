using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Explicit)]
public struct RkllmInputUnion {
    [FieldOffset(0)]
    public IntPtr PromptInput;

    [FieldOffset(0)]
    public RkllmTokenInput TokenInput;

    [FieldOffset(0)]
    public RkllmMultimodalInput MultimodalInput;
}
