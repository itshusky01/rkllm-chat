using Microsoft.Extensions.Configuration;

namespace RkllmChat.Configuration;

public static class AppConfigLoader {
    public static ApplicationConfig Load(string[]? args = null) {
        var configPath = ResolveConfigPath(args);
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(configPath)!)
            .AddJsonFile(Path.GetFileName(configPath), optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var config = configuration.Get<ApplicationConfig>()
            ?? throw new InvalidOperationException($"Unable to bind application configuration from '{configPath}'.");

        NormalizePaths(config, Path.GetDirectoryName(configPath)!);
        return config;
    }

    private static void NormalizePaths(ApplicationConfig config, string baseDirectory) {
        if (!Path.IsPathRooted(config.Rkllm.ModelPath) && !string.IsNullOrWhiteSpace(config.Rkllm.ModelPath)) {
            config.Rkllm.ModelPath = Path.GetFullPath(Path.Combine(baseDirectory, config.Rkllm.ModelPath));
        }

        if (config.Rkllm.VlModel is { Path.Length: > 0 } vlModel && !Path.IsPathRooted(vlModel.Path)) {
            vlModel.Path = Path.GetFullPath(Path.Combine(baseDirectory, vlModel.Path));
        }
    }

    private static string ResolveConfigPath(string[]? args) {
        const string fileName = "config.json";

        var overridePath = TryGetConfigPathOverride(args);
        if (!string.IsNullOrWhiteSpace(overridePath)) {
            var resolvedPath = Path.IsPathRooted(overridePath)
                ? overridePath
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), overridePath));

            if (File.Exists(resolvedPath)) {
                return resolvedPath;
            }

            throw new FileNotFoundException($"Unable to find config file '{resolvedPath}'.");
        }

        var applicationDirectoryPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(applicationDirectoryPath)) {
            return applicationDirectoryPath;
        }

        var currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        if (File.Exists(currentDirectoryPath)) {
            return currentDirectoryPath;
        }

        throw new FileNotFoundException($"Unable to find {fileName} in the application directory or current working directory.");
    }

    private static string? TryGetConfigPathOverride(string[]? args) {
        if (args is null || args.Length == 0) {
            return null;
        }

        for (var i = 0; i < args.Length; i++) {
            var arg = args[i];

            if (string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase)) {
                if (i + 1 < args.Length) {
                    return args[i + 1];
                }

                throw new ArgumentException("Missing config file path after -c/--config.");
            }

            if (arg.StartsWith("-c=", StringComparison.OrdinalIgnoreCase)) {
                return arg[3..];
            }

            if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase)) {
                return arg[9..];
            }
        }

        return null;
    }
}
