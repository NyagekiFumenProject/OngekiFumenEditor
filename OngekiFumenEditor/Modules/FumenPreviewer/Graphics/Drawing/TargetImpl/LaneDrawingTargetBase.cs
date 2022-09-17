using Caliburn.Micro;
using Microsoft.VisualBasic;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    public abstract class LaneDrawingTargetBase<T> : CommonLinesDrawTargetBase<T> where T : ConnectableStartObject
    {
        private ITextureDrawing textureDrawing;
        private IPerfomenceMonitor perfomenceMonitor;

        public Texture StartEditorTexture { get; protected set; }
        public Texture NextEditorTexture { get; protected set; }
        public Texture EndEditorTexture { get; protected set; }

        private Vector2 editorSize = new(16, 16);

        public LaneDrawingTargetBase()
        {
            textureDrawing = IoC.Get<ITextureDrawing>();
            perfomenceMonitor = IoC.Get<IPerfomenceMonitor>();
        }

        public override void DrawBatch(IFumenPreviewer target, IEnumerable<T> objs)
        {
            base.DrawBatch(target, objs);
            perfomenceMonitor.OnBeginTargetDrawing(this);
            {
                void drawEditorTap(Texture texture, Vector2 size, IEnumerable<ConnectableObjectBase> o)
                {
                    using var d = o.Select(x => new
                    {
                        Obj = x,
                        Pos = new Vector2(
                            (float)XGridCalculator.ConvertXGridToX(x.XGrid, 30, target.ViewWidth, 1),
                            (float)TGridCalculator.ConvertTGridToY(x.TGrid, target.Fumen.BpmList, 1.0, 240)
                        )
                    }).Where(x => !(x.Pos.Y < target.CurrentPlayTime || x.Pos.Y > target.CurrentPlayTime + target.ViewHeight)).ToListWithObjectPool(out var list);

                    textureDrawing.Draw(target, texture, list.Select(x => (size, x.Pos, 0f)));
                }

                drawEditorTap(StartEditorTexture, editorSize, objs);
                drawEditorTap(NextEditorTexture, editorSize, objs.SelectMany(x => x.Children.OfType<ConnectableNextObject>()));
                drawEditorTap(EndEditorTexture, editorSize, objs.Select(x => x.Children.LastOrDefault()).OfType<ConnectableEndObject>());
            }
            perfomenceMonitor.OnAfterTargetDrawing(this);
        }
    }

    public abstract class LaneDrawingTargetBase : LaneDrawingTargetBase<LaneStartBase>
    {

    }
}
