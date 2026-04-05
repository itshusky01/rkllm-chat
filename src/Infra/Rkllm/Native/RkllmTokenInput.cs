using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmTokenInput {
    public IntPtr InputIds;
    public nuint TokenCount;
}
