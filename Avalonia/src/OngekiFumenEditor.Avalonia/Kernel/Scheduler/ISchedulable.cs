namespace OngekiFumenEditor.Avalonia.Kernel.Scheduler;

public interface ISchedulable
{
    string SchedulerName { get; }
    void OnSchedulerTerm();
    TimeSpan ScheduleCallLoopInterval { get; }
    Task OnScheduleCall(CancellationToken cancellationToken);
}

