using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLogOutputWrapper output;
    private readonly DateTime startTime;

    public FileLoggerProvider(IEnumerable<ILogOutput> outputs)
    {
        output = outputs.OfType<FileLogOutputWrapper>().Single();
        startTime = DateTime.Now;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, output, startTime);
    }

    public void Dispose()
    {
    }
}
