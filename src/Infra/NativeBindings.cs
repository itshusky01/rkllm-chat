using System.Runtime.InteropServices;

namespace RKLLM.Infra;

public static class NativeBindings {
    public const string LibraryName = "librkllmrt.so";

    public enum LLMCallState: int {
        Normal = 0,
        Waiting = 1,
        Finish = 2,
        Error = 3
    }

    public enum RKLLMInputType: int {
        Prompt = 0,
        Token = 1,
        Embed = 2,
        Multimodal = 3
    }

    public enum RKLLMInferMode: int {
        Generate = 0,
        GetLastHiddenLayer = 1,
        GetLogits = 2
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int RKLLMResultCallback(IntPtr result, IntPtr userData, LLMCallState state);

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMExtendParam {
        public int BaseDomainId;
        public sbyte EmbedFlash;
        public sbyte EnabledCpusNum;
        public uint EnabledCpusMask;
        public byte NBatch;
        public sbyte UseCrossAttn;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 104)]
        public byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMParam {
        public IntPtr ModelPath;
        public int MaxContextLen;
        public int MaxNewTokens;
        public int TopK;
        public int NKeep;
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

        public IntPtr ImgStart;
        public IntPtr ImgEnd;
        public IntPtr ImgContent;
        public RKLLMExtendParam ExtendParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMLoraAdapter {
        public IntPtr LoraAdapterPath;
        public IntPtr LoraAdapterName;
        public float Scale;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMEmbedInput {
        public IntPtr Embed;
        public nuint NTokens;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMTokenInput {
        public IntPtr InputIds;
        public nuint NTokens;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMMultiModalInput {
        public IntPtr Prompt;
        public IntPtr ImageEmbed;
        public nuint NImageTokens;
        public nuint NImage;
        public nuint ImageWidth;
        public nuint ImageHeight;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct RKLLMInputUnion {
        [FieldOffset(0)]
        public IntPtr PromptInput;

        [FieldOffset(0)]
        public RKLLMEmbedInput EmbedInput;

        [FieldOffset(0)]
        public RKLLMTokenInput TokenInput;

        [FieldOffset(0)]
        public RKLLMMultiModalInput MultimodalInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMInput {
        public IntPtr Role;

        [MarshalAs(UnmanagedType.I1)]
        public bool EnableThinking;

        public RKLLMInputType InputType;
        public RKLLMInputUnion InputData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMLoraParam {
        public IntPtr LoraAdapterName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMPromptCacheParam {
        public int SavePromptCache;
        public IntPtr PromptCachePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMInferParam {
        public RKLLMInferMode Mode;
        public IntPtr LoraParams;
        public IntPtr PromptCacheParams;
        public int KeepHistory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMResultLastHiddenLayer {
        public IntPtr HiddenStates;
        public int EmbdSize;
        public int NumTokens;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMResultLogits {
        public IntPtr Logits;
        public int VocabSize;
        public int NumTokens;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMPerfStat {
        public float PrefillTimeMs;
        public int PrefillTokens;
        public float GenerateTimeMs;
        public int GenerateTokens;
        public float MemoryUsageMb;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RKLLMResult {
        public IntPtr Text;
        public int TokenId;
        public RKLLMResultLastHiddenLayer LastHiddenLayer;
        public RKLLMResultLogits Logits;
        public RKLLMPerfStat Perf;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_init")]
    public static extern int rkllm_init(ref IntPtr handle, ref RKLLMParam param, RKLLMResultCallback callback);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_run")]
    public static extern int rkllm_run(IntPtr handle, ref RKLLMInput input, ref RKLLMInferParam inferParam, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_set_chat_template")]
    public static extern int rkllm_set_chat_template(IntPtr handle, IntPtr systemPrompt, IntPtr userPrompt, IntPtr assistantPrompt);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_set_function_tools")]
    public static extern int rkllm_set_function_tools(IntPtr handle, IntPtr systemPrompt, IntPtr tools, IntPtr toolResponse);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_destroy")]
    public static extern int rkllm_destroy(IntPtr handle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_abort")]
    public static extern int rkllm_abort(IntPtr handle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_load_lora")]
    public static extern int rkllm_load_lora(IntPtr handle, ref RKLLMLoraAdapter adapter);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rkllm_load_prompt_cache")]
    public static extern int rkllm_load_prompt_cache(IntPtr handle, IntPtr promptCachePath);
}