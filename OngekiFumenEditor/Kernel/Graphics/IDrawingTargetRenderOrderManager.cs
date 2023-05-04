using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IDrawingTargetRenderOrderManager
    {
        void SortRenderTarget(IDrawingTarget[] targets);

        void SetRenderOrder(IDrawingTarget drawingTargetType, int newOrder);
        void SetRenderOrder(IDrawingTarget drawingTargetType, IDrawingTarget relativeTargetType, int offset);
        void SetRenderOrder(IDrawingTarget drawingTargetType, IDrawingTarget aboveTargetType, IDrawingTarget underTargetType);
    }
}
