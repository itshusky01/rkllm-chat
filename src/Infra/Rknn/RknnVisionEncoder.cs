using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RkllmChat.Infra.Rknn.Native;

namespace RkllmChat.Infra.Rknn;

public sealed class RknnVisionEncoder : IDisposable {
    private ulong _context;
    private bool _isLoaded;
    private readonly ILogger _logger;
    private readonly List<string> _outputTensorSummaries = [];

    public uint ModelWidth { get; private set; }
    public uint ModelHeight { get; private set; }
    public uint ModelChannel { get; private set; }
    public uint ImageTokenCount { get; private set; }
    public uint EmbeddingSize { get; private set; }
    public uint OutputCount { get; private set; }

    public bool IsLoaded => _isLoaded;

    public RknnVisionEncoder(ILogger logger) {
        _logger = logger;
    }

    public bool Initialize(string modelPath, uint coreNum) {
        if (_isLoaded) {
            _logger.LogDebug("RKNN vision encoder is already initialized. Skipping reinitialization.");
            return true;
        }

        _logger.LogDebug("Initializing RKNN vision encoder. ModelPath={ModelPath}, CoreNum={CoreNum}", modelPath, coreNum);

        IntPtr modelPathPtr = Marshal.StringToHGlobalAnsi(modelPath);
        try {
            int ret = NativeMethods.Initialize(out _context, modelPathPtr, 0, 0, IntPtr.Zero);
            if (ret != 0) {
                _logger.LogError("RKNN Initialize failed: ret={Ret} path={Path}", ret, modelPath);
                return false;
            }
        }
        finally {
            Marshal.FreeHGlobal(modelPathPtr);
        }

        NativeMethods.SetCoreMask(_context, (RknnCoreMask)coreNum);
        _logger.LogDebug("RKNN core mask set to {CoreMask}", (RknnCoreMask)coreNum);

        if (!QueryInputOutputNumber()) return false;
        if (!QueryInputAttributes()) return false;
        if (!QueryOutputAttributes()) return false;

        if (_outputTensorSummaries.Count > 0) {
            _logger.LogDebug("RKNN output tensor shapes:\n{TensorShapes}", string.Join(Environment.NewLine, _outputTensorSummaries));
        }

        _logger.LogInformation("Vision encoder initialized. Input={W}x{H}x{C}, Tokens={T}, Embedding={E}, Outputs={O}",
            ModelWidth, ModelHeight, ModelChannel, ImageTokenCount, EmbeddingSize, OutputCount);

        _isLoaded = true;
        return true;
    }

    public unsafe float[]? Run(byte[] imageData) {
        if (!_isLoaded) return null;

        _logger.LogDebug("Running RKNN encoder. InputBytes={InputBytes}, ExpectedInputShape={Width}x{Height}x{Channel}", imageData.Length, ModelWidth, ModelHeight, ModelChannel);

        fixed (byte* pInput = imageData) {
            var input = new RknnInput {
                Index = 0,
                Type = RknnTensorType.Uint8,
                Format = RknnTensorFormat.Nhwc,
                Size = ModelWidth * ModelHeight * ModelChannel,
                Buffer = (IntPtr)pInput,
                PassThrough = 0
            };

            if (NativeMethods.SetInputs(_context, 1, new[] { input }) != 0) {
                _logger.LogError("RKNN SetInputs failed.");
                return null;
            }
        }

        if (NativeMethods.Run(_context, IntPtr.Zero) != 0) {
            _logger.LogError("RKNN Run failed.");
            return null;
        }

        var outputs = new RknnOutput[OutputCount];
        for (int i = 0; i < OutputCount; i++) {
            outputs[i].Index = (uint)i;
            outputs[i].WantFloat = 1;
        }

        if (NativeMethods.GetOutputs(_context, OutputCount, outputs, IntPtr.Zero) != 0) {
            _logger.LogError("RKNN GetOutputs failed.");
            return null;
        }

        try {
            int totalEmbedElements = (int)(ImageTokenCount * EmbeddingSize * OutputCount);
            _logger.LogDebug("RKNN output ready. OutputCount={OutputCount}, ImageTokenCount={ImageTokenCount}, EmbeddingSize={EmbeddingSize}, TotalElements={TotalEmbedElements}", OutputCount, ImageTokenCount, EmbeddingSize, totalEmbedElements);
            float[] result = new float[totalEmbedElements];

            if (OutputCount == 1) {
                Marshal.Copy(outputs[0].Buffer, result, 0, totalEmbedElements);
            }
            else {
                var resultSpan = result.AsSpan();
                int floatSize = sizeof(float);

                for (int i = 0; i < (int)ImageTokenCount; i++) {
                    for (int j = 0; j < (int)OutputCount; j++) {
                        int destOffset = (int)((i * OutputCount + j) * EmbeddingSize);
                        IntPtr srcPtr = outputs[j].Buffer + (int)(i * EmbeddingSize * floatSize);
                        var srcSpan = new ReadOnlySpan<float>((void*)srcPtr, (int)EmbeddingSize);
                        srcSpan.CopyTo(resultSpan.Slice(destOffset, (int)EmbeddingSize));
                    }
                }
            }
            return result;
        }
        finally {
            NativeMethods.ReleaseOutputs(_context, OutputCount, outputs);
        }
    }

    private bool QueryInputOutputNumber() {
        int size = Marshal.SizeOf<RknnInputOutputNum>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try {
            unsafe { Unsafe.InitBlock((void*)ptr, 0, (uint)size); }

            if (NativeMethods.Query(_context, RknnQueryCommand.InputOutputNumber, ptr, (uint)size) != 0) return false;
            var ioNum = Marshal.PtrToStructure<RknnInputOutputNum>(ptr);
            OutputCount = ioNum.OutputCount;
            return true;
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    private bool QueryInputAttributes() {
        var attr = new RknnTensorAttribute { Index = 0 };
        int size = Marshal.SizeOf<RknnTensorAttribute>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try {
            Marshal.StructureToPtr(attr, ptr, false);

            if (NativeMethods.Query(_context, RknnQueryCommand.InputAttribute, ptr, (uint)size) != 0) return false;

            attr = Marshal.PtrToStructure<RknnTensorAttribute>(ptr);
            if (attr.Format == RknnTensorFormat.Nchw) {
                ModelChannel = attr.Dimensions[1];
                ModelHeight = attr.Dimensions[2];
                ModelWidth = attr.Dimensions[3];
            } else {
                ModelHeight = attr.Dimensions[1];
                ModelWidth = attr.Dimensions[2];
                ModelChannel = attr.Dimensions[3];
            }
            return true;
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    private bool QueryOutputAttributes() {
        _outputTensorSummaries.Clear();

        for (uint outputIndex = 0; outputIndex < OutputCount; outputIndex++) {
            var attr = new RknnTensorAttribute {
                Index = outputIndex,
                Dimensions = new uint[16],
                Name = string.Empty
            };

            int size = Marshal.SizeOf<RknnTensorAttribute>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try {
                Marshal.StructureToPtr(attr, ptr, false);

                if (NativeMethods.Query(_context, RknnQueryCommand.OutputAttribute, ptr, (uint)size) != 0) {
                    return false;
                }

                attr = Marshal.PtrToStructure<RknnTensorAttribute>(ptr);
                var summary = BuildOutputTensorSummary(attr);
                _outputTensorSummaries.Add(summary);

                if (outputIndex == 0 && attr.Dimensions is { Length: > 1 }) {
                    for (int i = 0; i < 4 && i + 1 < attr.Dimensions.Length; i++) {
                        if (attr.Dimensions[i] > 1) {
                            ImageTokenCount = attr.Dimensions[i];
                            EmbeddingSize = attr.Dimensions[i + 1];
                            break;
                        }
                    }
                }
            }
            finally {
                Marshal.FreeHGlobal(ptr);
            }
        }

        return true;
    }

    private static string BuildOutputTensorSummary(RknnTensorAttribute attr) {
        var tensorName = string.IsNullOrWhiteSpace(attr.Name) ? "<unnamed>" : attr.Name;
        var shape = FormatDimensions(attr.Dimensions, attr.DimensionCount);
        return $"Output[{attr.Index}] Name={tensorName}, Shape=[{shape}], Format={attr.Format}, Type={attr.Type}, Elements={attr.ElementCount}, Bytes={attr.Size}";
    }

    private static string FormatDimensions(uint[]? dimensions, uint dimensionCount) {
        if (dimensions is null || dimensions.Length == 0 || dimensionCount == 0) {
            return "unknown";
        }

        var actualCount = Math.Min((int)dimensionCount, dimensions.Length);
        var parts = new string[actualCount];
        for (var i = 0; i < actualCount; i++) {
            parts[i] = dimensions[i].ToString();
        }

        return string.Join("x", parts);
    }

    public void Dispose() {
        if (_context != 0) {
            NativeMethods.Destroy(_context);
            _context = 0;
            _isLoaded = false;
        }
        GC.SuppressFinalize(this);
    }
}
