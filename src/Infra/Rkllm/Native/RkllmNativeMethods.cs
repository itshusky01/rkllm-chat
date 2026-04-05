using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rkllm.Native;

internal static class RkllmNativeMethods {
    private const string LibraryName = "librkllmrt.so";

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_init")]
    internal static extern int Initialize(ref IntPtr handle, ref RkllmParam param, RkllmResultCallback callback);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_run")]
    internal static extern int Run(IntPtr handle, ref RkllmInput input, ref RkllmInferParam inferParam, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_set_chat_template")]
    internal static extern int SetChatTemplate(IntPtr handle, IntPtr systemPrompt, IntPtr userPrompt, IntPtr assistantPrompt);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_set_function_tools")]
    internal static extern int SetFunctionTools(IntPtr handle, IntPtr systemPrompt, IntPtr tools, IntPtr toolResponse);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_destroy")]
    internal static extern int Destroy(IntPtr handle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_abort")]
    internal static extern int Abort(IntPtr handle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_load_lora")]
    internal static extern int LoadLora(IntPtr handle, ref RkllmLoraAdapter adapter);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_load_prompt_cache")]
    internal static extern int LoadPromptCache(IntPtr handle, IntPtr promptCachePath);
}
