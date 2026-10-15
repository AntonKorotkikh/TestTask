using Serilog;

namespace TestTask;

internal static class LogConfiguration
{
    internal static Serilog.ILogger CreateLogger()
    {
        return new LoggerConfiguration()
            .WriteTo.Console(
                outputTemplate: "[{UtcTimestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File("log.txt",
                rollingInterval: RollingInterval.Month,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 200 * 1024 * 1024,
                outputTemplate: "{UtcTimestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
    }
}