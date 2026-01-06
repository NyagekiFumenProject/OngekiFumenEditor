using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private const string LoggerFolderPath = "Logs";
    private const string CurrentLogFilePath = "current.log";

    private readonly string[] logFiles;
    private readonly DateTime startTime;

    public FileLoggerProvider()
    {
        File.Delete(CurrentLogFilePath);
        startTime = DateTime.Now;
        Directory.CreateDirectory(LoggerFolderPath);
        var logFileName = $"app-{startTime:yyyy-MM-dd HH-mm-ss}.log";
        logFiles = new[]
        {
            Path.Combine(LoggerFolderPath, logFileName),
            CurrentLogFilePath
        };
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, logFiles, startTime);
    }

    public void Dispose()
    {
    }
}