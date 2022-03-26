using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils.Logs
{
    public interface ILogOutput
    {
        public void WriteLog(string content);
    }
}
