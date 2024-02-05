namespace OngekiFumenEditor.Utils.Logs
{
    public interface ILogOutput
    {
        public enum Severity
        {
            Debug,
            Info,
            Warn,
            Error
        }

        public void WriteLog(Severity severity, string content);
    }
}
