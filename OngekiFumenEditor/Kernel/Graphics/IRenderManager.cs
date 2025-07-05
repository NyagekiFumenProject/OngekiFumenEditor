using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IRenderManager
    {
        IRenderManagerImpl GetCurrentRenderManagerImpl();

        IEnumerable<string> GetAvaliableRenderManagerImplNames();
        void SetRenderManagerImpl(string implName);
    }
}
