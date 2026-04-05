using System.Runtime.InteropServices;

namespace RkllmChat.Infra.Rknn.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct RknnTensorAttribute {
    public uint Index;
    public uint DimensionCount;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public uint[] Dimensions;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Name;

    public uint ElementCount;
    public uint Size;
    public RknnTensorFormat Format;
    public RknnTensorType Type;
    public uint QuantizationType;
    public sbyte FractionalLength;
    public int ZeroPoint;
    public float Scale;
    public uint WidthStride;
    public uint SizeWithStride;
    public byte PassThrough;
    public uint HeightStride;
}
