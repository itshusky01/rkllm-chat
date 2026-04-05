namespace RkllmChat.Infra.Rknn.Native;

public enum RknnTensorType : uint {
    Float32 = 0,
    Float16,
    Int8,
    Uint8,
    Int16,
    Uint16,
    Int32,
    Uint32,
    Int64,
    Bool,
    Max
}
