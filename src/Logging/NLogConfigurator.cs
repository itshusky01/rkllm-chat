using NLog;
using NLog.Config;
using NLog.Targets;

namespace RkllmChat.Logging;

internal static class NLogConfigurator {
    public static void Configure(bool enableDebugLogging = false) {
        var config = new LoggingConfiguration();

        var consoleTarget = new ColoredConsoleTarget("console") {
            Layout = "[${date:format=HH\\:mm\\:ss.fff}] [${logger:shortName=true}] [${uppercase:${level}}] - ${message}${onexception:inner=${newline}${exception:format=tostring}}",
            UseDefaultRowHighlightingRules = true
        };

        config.AddTarget(consoleTarget);
        config.AddRule(enableDebugLogging ? LogLevel.Debug : LogLevel.Info, LogLevel.Fatal, consoleTarget, "RkllmChat.*");
        config.AddRule(LogLevel.Warn, LogLevel.Fatal, consoleTarget, "Microsoft.*");

        LogManager.Configuration = config;
        LogManager.ReconfigExistingLoggers();
    }
}
