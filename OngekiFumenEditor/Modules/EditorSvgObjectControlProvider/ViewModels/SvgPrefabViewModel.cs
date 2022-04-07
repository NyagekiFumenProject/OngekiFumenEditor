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

        public Rect GeometryBound => ProcessingDrawingGroup?.Bounds ?? Rect.Empty;

        private Point point;
        public Point Point
        {
            get => point;
            set => Set(ref point, value);
        }

        private DrawingGroup processedDrawingGroup = default;
        public DrawingGroup ProcessingDrawingGroup
        {
            get => processedDrawingGroup;
            set
            {
                Set(ref processedDrawingGroup, value);
                NotifyOfPropertyChange(() => GeometryBound);
            }
        }

        protected override void OnOngekiObjectPropChanged(object sender, PropertyChangedEventArgs arg)
        {
            switch (arg.PropertyName)
            {
                case nameof(SvgPrefab.ProcessingDrawingGroup):
                    RebuildGeometry();
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

        private void RecalculatePoint()
        {
            var bound = RefSvgPrefab.ProcessingDrawingGroup.Bounds;
            Point = new Point()
            {
                X = -RefSvgPrefab.OffsetX.CurrentValue * bound.Width,
                Y = -RefSvgPrefab.OffsetY.CurrentValue * bound.Height
            };
        }

        private void RebuildGeometry()
        {
            ProcessingDrawingGroup = RefSvgPrefab.ProcessingDrawingGroup;
        }
    }
}
