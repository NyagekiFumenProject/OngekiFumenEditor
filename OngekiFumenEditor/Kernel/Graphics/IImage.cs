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

        /// <summary>
        /// Gets the image width in device pixels.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the image height in device pixels.
        /// </summary>
        int Height { get; }
    }
}
