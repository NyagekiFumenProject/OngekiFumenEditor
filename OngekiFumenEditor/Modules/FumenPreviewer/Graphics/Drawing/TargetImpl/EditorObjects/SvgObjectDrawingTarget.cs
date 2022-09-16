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
            var data = cachedSvgRenderDataManager.GetRenderData(target, obj);
            lineDrawing.Draw(target, data, 1);
        }
    }
}
