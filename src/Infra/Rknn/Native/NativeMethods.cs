using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rknn.Native;

internal static class NativeMethods {
    private const string LibraryName = "librknnrt";

    [DllImport(LibraryName, EntryPoint = "rknn_init", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Initialize(out ulong context, IntPtr model, uint size, uint flags, IntPtr extend);

    [DllImport(LibraryName, EntryPoint = "rknn_destroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Destroy(ulong context);

    [DllImport(LibraryName, EntryPoint = "rknn_set_core_mask", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetCoreMask(ulong context, RknnCoreMask coreMask);

    [DllImport(LibraryName, EntryPoint = "rknn_query", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Query(ulong context, RknnQueryCommand command, IntPtr info, uint size);

    [DllImport(LibraryName, EntryPoint = "rknn_inputs_set", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetInputs(ulong context, uint inputCount, [In] RknnInput[] inputs);

    [DllImport(LibraryName, EntryPoint = "rknn_run", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Run(ulong context, IntPtr extend);

    [DllImport(LibraryName, EntryPoint = "rknn_outputs_get", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetOutputs(ulong context, uint outputCount, [In, Out] RknnOutput[] outputs, IntPtr extend);

    [DllImport(LibraryName, EntryPoint = "rknn_outputs_release", CallingConvention = CallingConvention.Cdecl)]
    public static extern int ReleaseOutputs(ulong context, uint outputCount, [In] RknnOutput[] outputs);

    [DllImport(LibraryName, EntryPoint = "rknn_create_mem", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CreateMemory(ulong context, uint size);

    [DllImport(LibraryName, EntryPoint = "rknn_destroy_mem", CallingConvention = CallingConvention.Cdecl)]
    public static extern int DestroyMemory(ulong context, IntPtr memory);
}
