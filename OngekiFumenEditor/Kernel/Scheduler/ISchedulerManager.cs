using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Scheduler
{
    public interface ISchedulerManager
    {
        Task Init();
        Task AddScheduler(ISchedulable s);
        Task RemoveScheduler(ISchedulable s);
    }
}
