namespace RkllmChat.Infra.Rknn.Native;

public enum RknnQueryCommand : uint {
    InputOutputNumber = 0,
    InputAttribute = 1,
    OutputAttribute = 2,
    PerformanceDetail = 3,
    PerformanceRun = 4,
    SdkVersion = 5,
    MemorySize = 6,
    CustomString = 7,
    NativeInputAttribute = 8,
    NativeOutputAttribute = 9,
    DeviceMemoryInfo = 12,
    CurrentInputAttribute = 14,
    CurrentOutputAttribute = 15
}
