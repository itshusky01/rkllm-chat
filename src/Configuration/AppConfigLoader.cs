using Microsoft.Extensions.Configuration;

namespace RkllmChat.Configuration;

public static class AppConfigLoader {
    public static ApplicationConfig Load() {
        var configPath = ResolveConfigPath();
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

        if (!Path.IsPathRooted(config.Rkllm.TempImageDirectory) && !string.IsNullOrWhiteSpace(config.Rkllm.TempImageDirectory)) {
            config.Rkllm.TempImageDirectory = Path.GetFullPath(Path.Combine(baseDirectory, config.Rkllm.TempImageDirectory));
        }
    }

    private static string ResolveConfigPath() {
        const string fileName = "config.json";

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
}
