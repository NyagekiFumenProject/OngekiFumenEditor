using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ArgProcesser
{
    public interface IProgramArgProcessManager
    {
        Task ProcessArgs(string[] args);
    }
}
