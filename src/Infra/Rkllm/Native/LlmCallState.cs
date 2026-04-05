namespace RkllmChat.Infra.Rkllm.Native;

public enum LlmCallState : int {
    Normal = 0,
    Waiting = 1,
    Finish = 2,
    Error = 3
}
