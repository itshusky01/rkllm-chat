namespace RkllmChat.Dtos;

public sealed class ProfilerMemoryStats {
    public double WorkingSetMb { get; set; }
    public double PrivateMemoryMb { get; set; }
    public double ManagedHeapMb { get; set; }
    public double GcTotalAvailableMb { get; set; }
    public double? SystemTotalMb { get; set; }
    public double? SystemAvailableMb { get; set; }
}
