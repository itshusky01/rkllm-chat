using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RkllmChat.Configuration;
using RkllmChat.Dtos;
using RkllmChat.Infra.Rkllm;
using RkllmChat.Infra.Rknn;

namespace RkllmChat.Controllers;

public static class ProfilerEndpoints {
    public static IEndpointRouteBuilder MapProfilerEndpoints(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/api/profiler", GetProfiler);
        endpoints.MapGet("/api/stats", GetProfiler);
        return endpoints;
    }

    private static IResult GetProfiler(
        AppOptions options,
        RkllmWrapper runtime,
        ChatInferenceService inferenceService,
        IServiceProvider serviceProvider) {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var now = DateTimeOffset.UtcNow;
        var startTime = GetProcessStartTime(process);
        var uptime = now - startTime;
        var totalProcessorTime = process.TotalProcessorTime;
        var cpuPercent = 0d;

        if (uptime > TimeSpan.Zero && Environment.ProcessorCount > 0) {
            cpuPercent = Math.Round(
                totalProcessorTime.TotalMilliseconds / (uptime.TotalMilliseconds * Environment.ProcessorCount) * 100d,
                2);
        }

        var gcInfo = GC.GetGCMemoryInfo();
        var requestStats = inferenceService.GetStatsSnapshot();
        var runtimePerf = runtime.GetLastPerformance();
        var (systemTotalMb, systemAvailableMb) = TryGetSystemMemoryMb();

        var vlModel = options.VlModel;
        var visionEncoder = serviceProvider.GetService<RknnVisionEncoder>();

        return TypedResults.Ok(new ProfilerResponse {
            Service = "RkllmChat",
            Timestamp = now,
            UptimeSeconds = Math.Round(uptime.TotalSeconds, 2),
            Host = options.Server.Host,
            Port = options.Server.Port,
            Model = options.ModelPath,
            Platform = options.Platform,
            HasVlModel = vlModel is not null,
            System = new ProfilerSystemInfo {
                ProcessId = Environment.ProcessId,
                MachineName = Environment.MachineName,
                OsDescription = RuntimeInformation.OSDescription,
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessorCount = Environment.ProcessorCount
            },
            Cpu = new ProfilerCpuStats {
                ProcessUsagePercent = cpuPercent,
                TotalProcessorTimeMs = Math.Round(totalProcessorTime.TotalMilliseconds, 2),
                ThreadCount = TryGetThreadCount(process)
            },
            Memory = new ProfilerMemoryStats {
                WorkingSetMb = ToMegabytes(process.WorkingSet64),
                PrivateMemoryMb = ToMegabytes(process.PrivateMemorySize64),
                ManagedHeapMb = ToMegabytes(GC.GetTotalMemory(forceFullCollection: false)),
                GcTotalAvailableMb = gcInfo.TotalAvailableMemoryBytes > 0 ? ToMegabytes(gcInfo.TotalAvailableMemoryBytes) : 0d,
                SystemTotalMb = systemTotalMb,
                SystemAvailableMb = systemAvailableMb
            },
            Requests = new ProfilerRequestStats {
                IsBusy = requestStats.IsBusy,
                Total = requestStats.TotalRequests,
                Completed = requestStats.CompletedRequests,
                Failed = requestStats.FailedRequests,
                Cancelled = requestStats.CancelledRequests,
                Rejected = requestStats.RejectedRequests,
                LastRequestStartedAt = requestStats.LastRequestStartedAt,
                LastRequestCompletedAt = requestStats.LastRequestCompletedAt,
                LastRequestDurationMs = Math.Round(requestStats.LastRequestDurationMs, 2),
                CurrentRequestAgeMs = Math.Round(requestStats.CurrentRequestAgeMs, 2),
                CurrentMode = requestStats.CurrentRequestMode,
                LastError = requestStats.LastError
            },
            Tokens = new ProfilerTokenStats {
                TotalInputTokens = requestStats.TotalEstimatedInputTokens,
                TotalOutputTokens = requestStats.TotalEstimatedOutputTokens,
                CurrentRequestInputTokens = requestStats.CurrentRequestInputTokens,
                CurrentRequestOutputTokens = requestStats.CurrentRequestOutputTokens,
                CurrentOutputChars = requestStats.CurrentOutputCharacters,
                CurrentChunkCount = requestStats.CurrentChunkCount,
                TotalChunksEmitted = requestStats.TotalChunksEmitted,
                LastRequestInputTokens = requestStats.LastRequestInputTokens,
                LastRequestOutputTokens = requestStats.LastRequestOutputTokens,
                CurrentTokensPerSecond = requestStats.CurrentTokensPerSecond,
                LastRequestTokensPerSecond = requestStats.LastRequestTokensPerSecond,
                AverageTokensPerSecond = requestStats.AverageTokensPerSecond
            },
            Runtime = new ProfilerRuntimeStats {
                State = runtime.GlobalState.ToString(),
                PrefillTimeMs = runtimePerf.PrefillTimeMs,
                PrefillTokens = runtimePerf.PrefillTokens,
                GenerateTimeMs = runtimePerf.GenerateTimeMs,
                GenerateTokens = runtimePerf.GenerateTokens,
                TokensPerSecond = runtimePerf.GenerateTimeMs > 0
                    ? Math.Round(runtimePerf.GenerateTokens / (runtimePerf.GenerateTimeMs / 1000d), 2)
                    : 0d,
                MemoryUsageMb = runtimePerf.MemoryUsageMb,
                LastUpdatedAt = runtime.LastPerformanceUpdatedAt
            },
            Vision = new ProfilerVisionStats {
                Enabled = vlModel is not null,
                Loaded = visionEncoder?.IsLoaded ?? false,
                ModelPath = vlModel?.Path,
                Width = visionEncoder is not null && visionEncoder.IsLoaded ? (int)visionEncoder.ModelWidth : vlModel?.Width,
                Height = visionEncoder is not null && visionEncoder.IsLoaded ? (int)visionEncoder.ModelHeight : vlModel?.Height,
                CoreMask = vlModel?.CoreMask
            }
        });
    }

    private static DateTimeOffset GetProcessStartTime(Process process) {
        try {
            return process.StartTime.ToUniversalTime();
        }
        catch {
            return DateTimeOffset.UtcNow;
        }
    }

    private static int TryGetThreadCount(Process process) {
        try {
            return process.Threads.Count;
        }
        catch {
            return 0;
        }
    }

    private static (double? TotalMb, double? AvailableMb) TryGetSystemMemoryMb() {
        const string MemInfoPath = "/proc/meminfo";
        if (!OperatingSystem.IsLinux() || !File.Exists(MemInfoPath)) {
            return (null, null);
        }

        try {
            long? totalKb = null;
            long? availableKb = null;

            foreach (var line in File.ReadLines(MemInfoPath)) {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase)) {
                    totalKb = ParseMemInfoValue(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase)) {
                    availableKb = ParseMemInfoValue(line);
                }

                if (totalKb.HasValue && availableKb.HasValue) {
                    break;
                }
            }

            return (
                totalKb.HasValue ? Math.Round(totalKb.Value / 1024d, 2) : null,
                availableKb.HasValue ? Math.Round(availableKb.Value / 1024d, 2) : null);
        }
        catch {
            return (null, null);
        }
    }

    private static long? ParseMemInfoValue(string line) {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out var value) ? value : null;
    }

    private static double ToMegabytes(long bytes) {
        return Math.Round(bytes / 1024d / 1024d, 2);
    }
}
