using Caliburn.Micro;
using OngekiFumenEditor.Base;

using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Polyline2DCSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    public abstract class CommonLinesDrawTargetBase<T> : CommonDrawTargetBase<T> where T : OngekiObjectBase
    {
        public virtual int LineWidth { get; } = 2;
        private ILineDrawing lineDrawing;

        public CommonLinesDrawTargetBase()
        {
            lineDrawing = IoC.Get<ILineDrawing>();
        }

        public abstract void FillLine(IFumenPreviewer target, List<LineVertex> list, T obj);

        public override void Draw(IFumenPreviewer target, T obj)
        {
            using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
            list.Clear();
            FillLine(target, list, obj);
            lineDrawing.Draw(target, list, LineWidth);
        }
    }
}
