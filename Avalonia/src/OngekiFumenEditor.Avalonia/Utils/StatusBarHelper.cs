namespace OngekiFumenEditor.Avalonia.Utils;

public static class StatusBarHelper
{
    public sealed class Notify(string statusDescription) : IDisposable
    {
        public string StatusDescription { get; } = statusDescription;

        public void Dispose()
        {
            EndStatus(this);
        }
    }

    private static readonly object statusLock = new();
    private static readonly List<Notify> currentStatusList = [];

    private static void UpdateStatusToStatusBar()
    {
        var firstStatus = currentStatusList.FirstOrDefault();
        var descStr = firstStatus?.StatusDescription ?? string.Empty;
        IoC.Get<CommonStatusBar>().SetMainMessage(descStr);
    }

    public static Notify BeginStatus(string statusDescription)
    {
        var notify = new Notify(statusDescription);
        lock (statusLock)
        {
            currentStatusList.Add(notify);
            UpdateStatusToStatusBar();
        }
        return notify;
    }

    public static void EndStatus(Notify notify)
    {
        lock (statusLock)
        {
            if (currentStatusList.Remove(notify))
                UpdateStatusToStatusBar();
        }
    }
}
