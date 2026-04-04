using System.Runtime.InteropServices;
using static RkllmChat.Infra.NativeBindings;

namespace RkllmChat.Infra;

public sealed class RkllmWrapper : IDisposable {
    private readonly RKLLMResultCallback _callback;
    private readonly List<IntPtr> _persistentAllocations = [];
    private IntPtr _handle;
    private bool _disposed;

    public LLMCallState GlobalState { get; private set; } = (LLMCallState)(-1);

    public event Action<string>? TextGenerated;
    public event Action<float[]>? EmbeddingGenerated;
    public event Action<LLMCallState>? StateChanged;

    public RkllmWrapper(string modelPath, string platform = "rk3588", int maxContextLen = 4096) {
        _callback = HandleCallback;

        var param = new RKLLMParam {
            ModelPath = AllocPersistentUtf8(modelPath),
            MaxContextLen = maxContextLen,
            MaxNewTokens = 8192,
            TopK = 1,
            NKeep = -1,
            TopP = 0.9f,
            Temperature = 0.8f,
            RepeatPenalty = 1.1f,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f,
            Mirostat = 0,
            MirostatTau = 5.0f,
            MirostatEta = 0.1f,
            SkipSpecialToken = true,
            IsAsync = false,
            ImgStart = AllocPersistentUtf8("<image>"),
            ImgEnd = AllocPersistentUtf8("</image>"),
            ImgContent = AllocPersistentUtf8(string.Empty),
            ExtendParam = new RKLLMExtendParam {
                BaseDomainId = 0,
                EmbedFlash = 1,
                EnabledCpusNum = 4,
                EnabledCpusMask = GetCpuMask(platform),
                NBatch = 1,
                UseCrossAttn = 0,
                Reserved = new byte[104]
            }
        };

        var ret = rkllm_init(ref _handle, ref param, _callback);
        if (ret != 0) {
            throw new InvalidOperationException($"rkllm_init failed with code {ret}.");
        }
    }

    public int Run(string prompt, string role = "user", bool enableThinking = false, bool keepHistory = false) {
        ThrowIfDisposed();

        var input = new RKLLMInput {
            Role = Marshal.StringToCoTaskMemUTF8(role),
            EnableThinking = enableThinking,
            InputType = RKLLMInputType.Prompt,
            InputData = new RKLLMInputUnion {
                PromptInput = Marshal.StringToCoTaskMemUTF8(prompt)
            }
        };

        var infer = new RKLLMInferParam {
            Mode = RKLLMInferMode.Generate,
            LoraParams = IntPtr.Zero,
            PromptCacheParams = IntPtr.Zero,
            KeepHistory = keepHistory ? 1 : 0
        };

        try {
            return rkllm_run(_handle, ref input, ref infer, IntPtr.Zero);
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

    public int GetEmbedding(string prompt, string role = "user") {
        ThrowIfDisposed();

        var input = new RKLLMInput {
            Role = Marshal.StringToCoTaskMemUTF8(role),
            EnableThinking = false,
            InputType = RKLLMInputType.Prompt,
            InputData = new RKLLMInputUnion {
                PromptInput = Marshal.StringToCoTaskMemUTF8(prompt)
            }
        };

        var infer = new RKLLMInferParam {
            Mode = RKLLMInferMode.GetLastHiddenLayer,
            LoraParams = IntPtr.Zero,
            PromptCacheParams = IntPtr.Zero,
            KeepHistory = 0
        };

        try {
            return rkllm_run(_handle, ref input, ref infer, IntPtr.Zero);
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

    public int SetChatTemplate(string systemPrompt, string userPrompt, string assistantPrompt) {
        ThrowIfDisposed();

        var systemPtr = Marshal.StringToCoTaskMemUTF8(systemPrompt);
        var userPtr = Marshal.StringToCoTaskMemUTF8(userPrompt);
        var assistantPtr = Marshal.StringToCoTaskMemUTF8(assistantPrompt);

        try {
            return rkllm_set_chat_template(_handle, systemPtr, userPtr, assistantPtr);
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
            return rkllm_set_function_tools(_handle, systemPtr, toolsPtr, responsePtr);
        }
        finally {
            Marshal.FreeCoTaskMem(systemPtr);
            Marshal.FreeCoTaskMem(toolsPtr);
            Marshal.FreeCoTaskMem(responsePtr);
        }
    }

    public int Abort() {
        ThrowIfDisposed();
        return rkllm_abort(_handle);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        if (_handle != IntPtr.Zero) {
            rkllm_destroy(_handle);
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

    private int HandleCallback(IntPtr resultPtr, IntPtr userData, LLMCallState state) {
        GlobalState = state;
        StateChanged?.Invoke(state);

        if (resultPtr == IntPtr.Zero) {
            return 0;
        }

        var result = Marshal.PtrToStructure<RKLLMResult>(resultPtr);

        if (state == LLMCallState.Normal && result.Text != IntPtr.Zero) {
            var text = Marshal.PtrToStringUTF8(result.Text);
            if (!string.IsNullOrEmpty(text)) {
                TextGenerated?.Invoke(text);
            }
        }

        if (state == LLMCallState.Finish &&
            result.LastHiddenLayer.HiddenStates != IntPtr.Zero &&
            result.LastHiddenLayer.EmbdSize > 0) {
            var embedding = new float[result.LastHiddenLayer.EmbdSize];
            Marshal.Copy(result.LastHiddenLayer.HiddenStates, embedding, 0, embedding.Length);
            EmbeddingGenerated?.Invoke(embedding);
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
