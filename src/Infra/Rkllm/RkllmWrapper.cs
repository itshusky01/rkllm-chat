using System.Runtime.InteropServices;
using RkllmChat.Configuration;
using RkllmChat.Infra.Rkllm.Native;

namespace RkllmChat.Infra.Rkllm;

public sealed class RkllmWrapper : IDisposable {
    private readonly RkllmResultCallback _callback;
    private readonly List<IntPtr> _persistentAllocations = [];
    private IntPtr _handle;
    private bool _disposed;

    public LlmCallState GlobalState { get; private set; } = (LlmCallState)(-1);

    public event Action<string>? TextGenerated;
    public event Action<LlmCallState>? StateChanged;

    public RkllmWrapper(string modelPath, string platform = "rk3588", int maxContextLen = 4096, LlmOptions? llmOptions = null) {
        _callback = HandleCallback;
        var llm = llmOptions ?? new LlmOptions();

        var param = new RkllmParam {
            ModelPath = AllocPersistentUtf8(modelPath),
            MaxContextLen = maxContextLen,
            MaxNewTokens = llm.MaxNewTokens,
            TopK = llm.TopK,
            TokensToKeep = -1,
            TopP = llm.TopP,
            Temperature = llm.Temperature,
            RepeatPenalty = llm.RepeatPenalty,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f,
            Mirostat = 0,
            MirostatTau = 5.0f,
            MirostatEta = 0.1f,
            SkipSpecialToken = true,
            IsAsync = false,
            ImageStart = AllocPersistentUtf8("<image>"),
            ImageEnd = AllocPersistentUtf8("</image>"),
            ImageContent = AllocPersistentUtf8(string.Empty),
            ExtendParam = new RkllmExtendParam {
                BaseDomainId = 0,
                EmbedFlash = 1,
                EnabledCpuCount = 4,
                EnabledCpuMask = GetCpuMask(platform),
                BatchSize = 1,
                UseCrossAttention = 0,
                Reserved = new byte[104]
            }
        };

        var ret = RkllmNativeMethods.Initialize(ref _handle, ref param, _callback);
        if (ret != 0) {
            throw new InvalidOperationException($"rkllm_init failed with code {ret}.");
        }
    }

    public int Run(string prompt, string role = "user", bool enableThinking = false, bool keepHistory = false) {
        ThrowIfDisposed();

        var input = new RkllmInput {
            Role = Marshal.StringToCoTaskMemUTF8(role),
            EnableThinking = enableThinking,
            InputType = RkllmInputType.Prompt,
            InputData = new RkllmInputUnion {
                PromptInput = Marshal.StringToCoTaskMemUTF8(prompt)
            }
        };

        var infer = new RkllmInferParam {
            Mode = RkllmInferMode.Generate,
            LoraParameters = IntPtr.Zero,
            PromptCacheParameters = IntPtr.Zero,
            KeepHistory = keepHistory ? 1 : 0
        };

        try {
            return RkllmNativeMethods.Run(_handle, ref input, ref infer, IntPtr.Zero);
        }
        finally {
            if (input.InputData.PromptInput != IntPtr.Zero) {
                Marshal.FreeCoTaskMem(input.InputData.PromptInput);
            }

            if (input.Role != IntPtr.Zero) {
                Marshal.FreeCoTaskMem(input.Role);
            }
        }
    }

    public unsafe int RunMultimodal(
        string prompt,
        byte[] embedData,
        int imageCount,
        int tokenCount,
        int width,
        int height,
        string role = "user",
        bool keepHistory = false) {
        ThrowIfDisposed();

        fixed (byte* pEmbed = embedData) {
            var vlmInput = new RkllmMultimodalInput {
                Prompt = Marshal.StringToCoTaskMemUTF8(prompt),
                ImageEmbedding = (float*) pEmbed,
                ImageTokenCount = (nuint) tokenCount,
                ImageCount = (nuint)imageCount,
                ImageWidth = (nuint)width,
                ImageHeight = (nuint)height
            };

            var input = new RkllmInput {
                Role = Marshal.StringToCoTaskMemUTF8(role),
                EnableThinking = false,
                InputType = RkllmInputType.Multimodal,
                InputData = new RkllmInputUnion {
                    MultimodalInput = vlmInput
                }
            };

            var infer = new RkllmInferParam {
                Mode = RkllmInferMode.Generate,
                LoraParameters = IntPtr.Zero,
                PromptCacheParameters = IntPtr.Zero,
                KeepHistory = keepHistory ? 1 : 0
            };

            try {
                return RkllmNativeMethods.Run(_handle, ref input, ref infer, IntPtr.Zero);
            }
            finally {
                if (vlmInput.Prompt != IntPtr.Zero) Marshal.FreeCoTaskMem(vlmInput.Prompt);
                if (input.Role != IntPtr.Zero) Marshal.FreeCoTaskMem(input.Role);
            }
        }
    }

    public int SetChatTemplate(string systemPrompt, string userPrompt, string assistantPrompt) {
        ThrowIfDisposed();

        var systemPtr = Marshal.StringToCoTaskMemUTF8(systemPrompt);
        var userPtr = Marshal.StringToCoTaskMemUTF8(userPrompt);
        var assistantPtr = Marshal.StringToCoTaskMemUTF8(assistantPrompt);

        try {
            return RkllmNativeMethods.SetChatTemplate(_handle, systemPtr, userPtr, assistantPtr);
        }
        finally {
            Marshal.FreeCoTaskMem(systemPtr);
            Marshal.FreeCoTaskMem(userPtr);
            Marshal.FreeCoTaskMem(assistantPtr);
        }
    }

    public int SetFunctionTools(string systemPrompt, string tools, string toolResponse) {
        ThrowIfDisposed();

        var systemPtr = Marshal.StringToCoTaskMemUTF8(systemPrompt);
        var toolsPtr = Marshal.StringToCoTaskMemUTF8(tools);
        var responsePtr = Marshal.StringToCoTaskMemUTF8(toolResponse);

        try {
            return RkllmNativeMethods.SetFunctionTools(_handle, systemPtr, toolsPtr, responsePtr);
        }
        finally {
            Marshal.FreeCoTaskMem(systemPtr);
            Marshal.FreeCoTaskMem(toolsPtr);
            Marshal.FreeCoTaskMem(responsePtr);
        }
    }

    public int Abort() {
        ThrowIfDisposed();
        return RkllmNativeMethods.Abort(_handle);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        if (_handle != IntPtr.Zero) {
            RkllmNativeMethods.Destroy(_handle);
            _handle = IntPtr.Zero;
        }

        foreach (var ptr in _persistentAllocations) {
            if (ptr != IntPtr.Zero) {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        _persistentAllocations.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~RkllmWrapper() {
        Dispose();
    }

    private int HandleCallback(IntPtr resultPtr, IntPtr userData, LlmCallState state) {
        GlobalState = state;
        StateChanged?.Invoke(state);

        if (resultPtr == IntPtr.Zero) {
            return 0;
        }

        var result = Marshal.PtrToStructure<RkllmResult>(resultPtr);

        if (state == LlmCallState.Normal && result.Text != IntPtr.Zero) {
            var text = Marshal.PtrToStringUTF8(result.Text);
            if (!string.IsNullOrEmpty(text)) {
                TextGenerated?.Invoke(text);
            }
        }

        return 0;
    }

    private IntPtr AllocPersistentUtf8(string? value) {
        var ptr = Marshal.StringToCoTaskMemUTF8(value ?? string.Empty);
        _persistentAllocations.Add(ptr);
        return ptr;
    }

    private static uint GetCpuMask(string platform) {
        return platform.ToLowerInvariant() switch {
            "rk3576" or "rk3588" => (1u << 4) | (1u << 5) | (1u << 6) | (1u << 7),
            _ => (1u << 0) | (1u << 1) | (1u << 2) | (1u << 3)
        };
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
