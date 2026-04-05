using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate int RkllmResultCallback(IntPtr resultPtr, IntPtr userData, LlmCallState state);
