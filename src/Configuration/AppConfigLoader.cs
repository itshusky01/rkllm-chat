namespace RKLLM.Configuration;

public static class AppConfigLoader {
    public static ApplicationConfig Load() {
        var configPath = ResolveConfigPath();
        var config = new ApplicationConfig();
        var section = string.Empty;

        foreach (var rawLine in File.ReadLines(configPath)) {
            if (string.IsNullOrWhiteSpace(rawLine)) {
                continue;
            }

            var trimmedLine = rawLine.Trim();
            if (trimmedLine.StartsWith('#')) {
                continue;
            }

            if (!char.IsWhiteSpace(rawLine[0]) && trimmedLine.EndsWith(':')) {
                section = trimmedLine[..^1];
                continue;
            }

            if (!string.Equals(section, "rkllm", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var separatorIndex = trimmedLine.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            var key = trimmedLine[..separatorIndex].Trim();
            var value = trimmedLine[(separatorIndex + 1)..].Trim().Trim('"', '\'');

            switch (key) {
                case "model_path":
                    config.Rkllm.ModelPath = value;
                    break;
                case "port" when int.TryParse(value, out var port):
                    config.Rkllm.Port = port;
                    break;
                case "platform":
                    config.Rkllm.Platform = value;
                    break;
                case "max_context_len" when int.TryParse(value, out var maxContextLength):
                    config.Rkllm.MaxContextLen = maxContextLength;
                    break;
            }
        }

        if (!Path.IsPathRooted(config.Rkllm.ModelPath) && !string.IsNullOrWhiteSpace(config.Rkllm.ModelPath)) {
            config.Rkllm.ModelPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath)!, config.Rkllm.ModelPath));
        }

        return config;
    }

    private static string ResolveConfigPath() {
        var applicationDirectoryPath = Path.Combine(AppContext.BaseDirectory, "config.yaml");
        if (File.Exists(applicationDirectoryPath)) {
            return applicationDirectoryPath;
        }

        var currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "config.yaml");
        if (File.Exists(currentDirectoryPath)) {
            return currentDirectoryPath;
        }

        throw new FileNotFoundException("Unable to find config.yaml in the application directory or current working directory.");
    }
}
