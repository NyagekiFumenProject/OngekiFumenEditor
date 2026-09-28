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
        /// 获取图像宽度（设备像素尺寸）。
        /// </summary>
        int Width { get; }

        /// <summary>
        /// 获取图像高度（设备像素尺寸）。
        /// </summary>
        int Height { get; }
    }
}
