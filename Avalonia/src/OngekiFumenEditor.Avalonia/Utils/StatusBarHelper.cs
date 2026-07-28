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

    private static readonly List<Notify> currentStatusList = [];

    private static void UpdateStatusToStatusBar()
    {
        var firstStatus = currentStatusList.FirstOrDefault();
        var descStr = firstStatus?.StatusDescription ?? string.Empty;
        IoC.Get<CommonStatusBar>().MainContentViewModel.Message = descStr;
    }

    public static Notify BeginStatus(string statusDescription)
    {
        var notify = new Notify(statusDescription);
        currentStatusList.Add(notify);
        UpdateStatusToStatusBar();
        return notify;
    }

    public static void EndStatus(Notify notify)
    {
        if (currentStatusList.Remove(notify))
            UpdateStatusToStatusBar();
    }
}
