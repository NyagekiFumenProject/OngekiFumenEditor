namespace OngekiFumenEditor.Avalonia.Utils.Logs;

public interface ILogOutput
{
    public enum Severity
    {
        Debug,
        Info,
        Warn,
        Error
    }

    void WriteLog(Severity severity, string content);
}

