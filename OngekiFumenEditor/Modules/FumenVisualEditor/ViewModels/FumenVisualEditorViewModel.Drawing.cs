using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Threading;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        private double xUnitSize = default;
        public double XUnitSize
        {
            get => xUnitSize;
            set => Set(ref xUnitSize, value);
        }

        protected void RecalculateXUnitSize()
        {
            XUnitSize = CanvasWidth / (Setting.XGridMaxUnit * 2) * Setting.UnitCloseSize;
        }

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            RedrawUnitCloseXLines();
        }

        private void RedrawTimeline()
        {
            foreach (var item in TGridUnitLineLocations)
                ObjectPool<TGridUnitLineViewModel>.Return(item);
            TGridUnitLineLocations.Clear();
            var baseLineAdded = false;

            foreach ((_, var bpm) in TGridCalculator.GetAllBpmUniformPositionList(this))
            {
                var nextBpm = Fumen.BpmList.GetNextBpm(bpm);
                var per = bpm.TGrid.ResT / Setting.BeatSplit;
                var i = 0;
                while (true)
                {
                    var tGrid = bpm.TGrid + new GridOffset(0, (int)(per * i));
                    if (nextBpm is not null && tGrid >= nextBpm.TGrid)
                        break;
                    var y = TGridCalculator.ConvertTGridToY(tGrid, this);
                    if (y > MaxVisibleCanvasY)
                        break;
                    var line = ObjectPool<TGridUnitLineViewModel>.Get();
                    line.TGrid = tGrid;
                    line.IsBaseLine = tGrid == Setting.CurrentDisplayTimePosition;
                    line.Y = CanvasHeight - y;

                    baseLineAdded = baseLineAdded || line.IsBaseLine;
                    TGridUnitLineLocations.Add(line);
                    i++;
                }
            }

            if (!baseLineAdded)
            {
                //添加一个基线表示当前时间轴
                if (TGridCalculator.ConvertTGridToY(Setting.CurrentDisplayTimePosition, this) is double y)
                {
                    var line = ObjectPool<TGridUnitLineViewModel>.Get();

                    line.TGrid = Setting.CurrentDisplayTimePosition;
                    line.IsBaseLine = true;
                    line.Y = CanvasHeight - y;

                    TGridUnitLineLocations.Add(line);
                }
            }
        }

        private void RedrawEditorObjects()
        {
            if (Fumen is null || CanvasHeight == 0)
                return;
            //var begin = TGridCalculator.ConvertYToTGrid(0, this) ?? new TGrid(0, 0);
            //var end = TGridCalculator.ConvertYToTGrid(CanvasHeight, this);

            //Log.LogDebug($"begin:({begin})  end:({end})  base:({Setting.CurrentDisplayTimePosition})");
            foreach (var item in EditorViewModels.OfType<DisplayObjectViewModelBase>())
                item.RecaulateCanvasXY();
        }

        private void RedrawUnitCloseXLines()
        {
            foreach (var item in XGridUnitLineLocations)
                ObjectPool<XGridUnitLineViewModel>.Return(item);
            XGridUnitLineLocations.Clear();

            var width = CanvasWidth;
            var unitSize = XUnitSize;
            var totalUnitValue = 0d;
            var line = default(XGridUnitLineViewModel);

            for (double totalLength = width / 2 + unitSize; totalLength < width; totalLength += unitSize)
            {
                totalUnitValue += Setting.UnitCloseSize;

                line = ObjectPool<XGridUnitLineViewModel>.Get();
                line.X = totalLength;
                line.Unit = totalUnitValue;
                line.IsCenterLine = false;
                XGridUnitLineLocations.Add(line);

                line = ObjectPool<XGridUnitLineViewModel>.Get();
                line.X = (width / 2) - (totalLength - (width / 2));
                line.Unit = -totalUnitValue;
                line.IsCenterLine = false;
                XGridUnitLineLocations.Add(line);
            }

            line = ObjectPool<XGridUnitLineViewModel>.Get();
            line.X = width / 2;
            line.IsCenterLine = true;
            XGridUnitLineLocations.Add(line);
        }

        public void Redraw(RedrawTarget target)
        {
            if (target.HasFlag(RedrawTarget.TGridUnitLines))
                RedrawTimeline();
            if (target.HasFlag(RedrawTarget.XGridUnitLines))
                RedrawUnitCloseXLines();
            if (target.HasFlag(RedrawTarget.OngekiObjects))
                RedrawEditorObjects();
            if (target.HasFlag(RedrawTarget.ScrollBar))
                RecalculateScrollBar();
        }

        public void OnLoaded(ActionExecutionContext e)
        {
            var view = e.View as FrameworkElement;
            CanvasWidth = view.ActualWidth;
            CanvasHeight = view.ActualHeight;
            Redraw(RedrawTarget.All);
        }

        public void OnSizeChanged(ActionExecutionContext e)
        {
            var arg = e.EventArgs as SizeChangedEventArgs;
            CanvasWidth = arg.NewSize.Width;
            CanvasHeight = arg.NewSize.Height;
            Redraw(RedrawTarget.All);
        }
    }
}
