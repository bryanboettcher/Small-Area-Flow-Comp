namespace SmallAreaFlowComp;

public interface ILogger
{
    void Debug(string message);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
    void Fatal(string message);
}

public class SimpleLogger : ILogger
{
    private readonly TextWriter _logOutput;
    private readonly LogLevel _logLevel;

    public SimpleLogger(TextWriter logOutput, LogLevel logLevel)
    {
        _logOutput = logOutput;
        _logLevel = logLevel;
    }

    public static ILogger Null => new SimpleLogger(TextWriter.Null, LogLevel.Fatal);

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Information(string message) => Log(LogLevel.Information, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Fatal(string message) => Log(LogLevel.Fatal, message);

    private void Log(LogLevel messageLogLevel, string message)
    {
        // smaller numbers are more severe, so we include
        // anything smaller than the requested log level
        if (messageLogLevel > _logLevel)
            return;

        var levelString = messageLogLevel switch
        {
            LogLevel.Fatal => "FATAL",
            LogLevel.Error => "ERROR",
            LogLevel.Warning => " WARN",
            LogLevel.Information => " INFO",
            LogLevel.Debug => "DEBUG",
            _ => throw new ArgumentOutOfRangeException(nameof(messageLogLevel), messageLogLevel, null)
        };

        var currentTime = DateTime.Now.ToString("G");

        _logOutput.WriteLine($"{currentTime} [{levelString}] {message}");
    }
}