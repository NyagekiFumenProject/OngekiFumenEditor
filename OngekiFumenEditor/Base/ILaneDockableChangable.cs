using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public interface ILaneDockableChangable : ILaneDockable
    {
        public int ReferenceLaneStrIdManualSet { get; set; }
    }
}
