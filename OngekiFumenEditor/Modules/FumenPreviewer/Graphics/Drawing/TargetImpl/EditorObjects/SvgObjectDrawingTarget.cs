using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached.DefaultImpl;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OpenTK.Mathematics;
using System.Windows.Media;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.EditorObjects
{
    [Export(typeof(IDrawingTarget))]
    public class SvgObjectDrawingTarget : CommonDrawTargetBase<SvgPrefabBase>
    {
        private ICachedSvgRenderDataManager cachedSvgRenderDataManager;
        private ILineDrawing lineDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { SvgStringPrefab.CommandName, SvgImageFilePrefab.CommandName };

        public SvgObjectDrawingTarget()
        {
            cachedSvgRenderDataManager = IoC.Get<ICachedSvgRenderDataManager>();
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
        }

        public override void Draw(IFumenPreviewer target, SvgPrefabBase obj)
        {
            if (obj.ProcessingDrawingGroup?.Bounds is not Rect bound)
                return;

            var w = bound.Width;
            var h = bound.Height;

            var x = (float)(XGridCalculator.ConvertXGridToX(obj.XGrid, 30, target.ViewWidth, 1) + w / 2);
            var y = (float)(TGridCalculator.ConvertTGridToY(obj.TGrid, target.Fumen.BpmList, 1.0, 240) - h / 2);

            var data = cachedSvgRenderDataManager.GetRenderData(target, obj);

            lineDrawing.PushOverrideModelMatrix(lineDrawing.GetOverrideModelMatrix() * Matrix4.CreateTranslation(x, y, 0));
            {
                lineDrawing.Draw(target, data, 1);
            }
            lineDrawing.PopOverrideModelMatrix(out _);
        }
    }
}
