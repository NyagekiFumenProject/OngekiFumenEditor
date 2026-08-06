namespace OngekiFumenEditor.Avalonia.Kernel.Scheduler;

public interface ISchedulerManager
{
    IEnumerable<ISchedulable> Schedulers { get; }
    Task Init();
    Task AddScheduler(ISchedulable s);
    Task RemoveScheduler(ISchedulable s);
    Task Term();
}

