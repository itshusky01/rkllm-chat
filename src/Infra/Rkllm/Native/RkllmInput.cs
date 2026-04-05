using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmInput {
    public IntPtr Role;

    [MarshalAs(UnmanagedType.I1)]
    public bool EnableThinking;

    public RkllmInputType InputType;
    public RkllmInputUnion InputData;
}
