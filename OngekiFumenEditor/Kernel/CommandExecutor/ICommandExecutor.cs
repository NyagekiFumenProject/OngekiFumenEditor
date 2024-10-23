using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.CommandExecutor
{
    public interface ICommandExecutor
    {
        Task<int> Execute(string[] args);
    }
}
