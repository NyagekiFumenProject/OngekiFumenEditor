using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using SharpVectors.Renderers.Wpf;
using SvgConverter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels
{
    [Gemini.Modules.Toolbox.ToolboxItem(typeof(FumenVisualEditorViewModel), "SvgPrefab", "Misc")]
    public class SvgPrefabViewModel : DisplayObjectViewModelBase<SvgPrefab>
    {
        public SvgPrefab RefSvgPrefab => (SvgPrefab)ReferenceOngekiObject;

        public Rect GeometryBound => RefSvgPrefab.ProcessingDrawingGroup?.Bounds ?? Rect.Empty;

        private Point point;
        public Point Point
        {
            get => point;
            set => Set(ref point, value);
        }

        protected override void OnOngekiObjectPropChanged(object sender, PropertyChangedEventArgs arg)
        {
            switch (arg.PropertyName)
            {
                case nameof(SvgPrefab.ProcessingDrawingGroup):
                    RecalculatePoint();
                    break;
                case nameof(SvgPrefab.OffsetX):
                case nameof(SvgPrefab.OffsetY):
                case nameof(RangeValue.CurrentValue):
                    RecalculatePoint();
                    break;
                default:
                    base.OnOngekiObjectPropChanged(sender, arg);
                    break;
            }
        }

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            base.OnObjectCreated(createFrom, editorViewModel);
            RecalculatePoint();
        }

        private void RecalculatePoint()
        {
            if (RefSvgPrefab.ProcessingDrawingGroup?.Bounds is Rect bound) {
                Point = new Point()
                {
                    X = -RefSvgPrefab.OffsetX.CurrentValue * bound.Width,
                    Y = -RefSvgPrefab.OffsetY.CurrentValue * bound.Height
                };
            }
        }
    }
}
