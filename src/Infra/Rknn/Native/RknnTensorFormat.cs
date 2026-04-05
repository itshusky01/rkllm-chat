namespace RkllmChat.Infra.Rknn.Native;

public enum RknnTensorFormat : uint {
    Nchw = 0,
    Nhwc,
    Nc1hwc2,
    Undefined
}
