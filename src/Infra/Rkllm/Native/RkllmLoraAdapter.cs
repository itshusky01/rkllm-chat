using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RkllmLoraAdapter {
    public IntPtr AdapterPath;
    public IntPtr AdapterName;
    public float Scale;
}
