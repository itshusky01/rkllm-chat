namespace RkllmChat.Infra.Rkllm.Native;

public enum RkllmInferMode : int {
    Generate = 0,
    GetLastHiddenLayer = 1,
    GetLogits = 2
}
