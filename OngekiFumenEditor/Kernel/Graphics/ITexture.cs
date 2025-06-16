using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IImage : IDisposable
    {
        TextureWrapMode TextureWrapT { get; set; }
        TextureWrapMode TextureWrapS { get; set; }
    }
}
