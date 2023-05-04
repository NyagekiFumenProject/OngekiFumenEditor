using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    [Export(typeof(IDrawingTargetRenderOrderManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultDrawingTargetRenderOrderManager : IDrawingTargetRenderOrderManager
    {
        private Dictionary<Type, int> orderMap = new();
        private IComparer<IDrawingTarget> comparer;

        private int GetOrder(IDrawingTarget drawingTarget)
        {
            var type = drawingTarget.GetType();
            if (!orderMap.TryGetValue(type, out var order))
                order = orderMap[type] = drawingTarget.DefaultRenderOrder;
            return order;
        }

        public DefaultDrawingTargetRenderOrderManager()
        {
            comparer = new ComparerWrapper<IDrawingTarget>((a, b) => GetOrder(a) - GetOrder(b));
        }

        private void SetRenderOrder(Type type, int newOrder)
        {
            orderMap[type] = newOrder;
        }

        public void SetRenderOrder(IDrawingTarget drawingTarget, int newOrder)
        {
            var type = drawingTarget.GetType();
            SetRenderOrder(type, newOrder);
        }

        public void SetRenderOrder(IDrawingTarget drawingTarget, IDrawingTarget relativeTarget, int offset)
        {
            var relativeBase = GetOrder(relativeTarget);
            SetRenderOrder(drawingTarget, relativeBase + offset);
        }

        public void SetRenderOrder(IDrawingTarget drawingTarget, IDrawingTarget aboveTarget, IDrawingTarget underTarget)
        {
            var above = GetOrder(aboveTarget);
            var under = GetOrder(underTarget);

            SetRenderOrder(drawingTarget, (above + under) / 2);
        }

        public void SortRenderTarget(IDrawingTarget[] targets)
        {
            Array.Sort(targets, comparer);
        }
    }
}
