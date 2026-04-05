namespace RkllmChat.Infra.Rknn.Native;

public enum RknnCoreMask : uint {
    NpuCoreAuto = 0,
    NpuCore0 = 1,
    NpuCore1 = 2,
    NpuCore2 = 4,
    NpuCore01 = NpuCore0 | NpuCore1,
    NpuCore02 = NpuCore0 | NpuCore2,
    NpuCore12 = NpuCore1 | NpuCore2,
    NpuCore012 = NpuCore0 | NpuCore1 | NpuCore2,
    Undefined = 0xFFFF
}
