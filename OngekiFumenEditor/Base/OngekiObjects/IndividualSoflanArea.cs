using System.Globalization;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class IndividualSoflanArea : OngekiMovableObjectBase
    {
        public class IndividualSoflanAreaEndIndicator : OngekiMovableObjectBase
        {
            public override string IDShortName => "[ISF_End]";

            public IndividualSoflanArea RefIndividualSoflanArea { get; internal protected set; }

            public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => System.Array.Empty<IDisplayableObject>();

            private TGrid tGrid;

            public override TGrid TGrid
            {
                get
                {
                    // null 表示终点尚未显式设置：此时终点跟随起点（零长度区域）。
                    if (tGrid is null)
                        SetTGridCore(RefIndividualSoflanArea.TGrid.CopyNew(), clampToStart: false);
                    return tGrid;
                }
                set => SetTGridCore(value, clampToStart: true);
            }

            internal void SetTGridUnclamped(TGrid value) => SetTGridCore(value, clampToStart: false);

            private void SetTGridCore(TGrid value, bool clampToStart)
            {
                var startTGrid = RefIndividualSoflanArea?.TGrid;
                var clamped = clampToStart && value is not null && startTGrid is not null && value < startTGrid
                    ? startTGrid.CopyNew()
                    : value;

                this.RegisterOrUnregisterPropertyChangeEvent(tGrid, clamped);
                tGrid = clamped;
                NotifyOfPropertyChange(() => TGrid);
            }

            public int SoflanGroup
            {
                get => RefIndividualSoflanArea.SoflanGroup;
                set
                {
                    RefIndividualSoflanArea.SoflanGroup = value;
                    NotifyOfPropertyChange(() => SoflanGroup);
                }
            }

            [ObjectPropertyBrowserShow]
            public float AreaWidth => RefIndividualSoflanArea.AreaWidth;

            public override string ToString() => $"{base.ToString()}";
            public override XGrid XGrid
            {
                get => base.XGrid;
                set
                {
                    base.XGrid = value;
                    NotifyOfPropertyChange(() => AreaWidth);
                }
            }
        }

        public override string IDShortName => "ISF";

        [ObjectPropertyBrowserShow]
        public float AreaWidth => (float)Math.Abs(XGrid.TotalUnit - EndIndicator.XGrid.TotalUnit);

        private int soflanGroup = 0;
        public int SoflanGroup
        {
            get => soflanGroup;
            set => Set(ref soflanGroup, value);
        }
        public override XGrid XGrid
        {
            get => base.XGrid;
            set
            {
                base.XGrid = value;
                NotifyOfPropertyChange(() => AreaWidth);
            }
        }
        public int GridLength => EndIndicator.TGrid.TotalGrid - TGrid.TotalGrid;

        /// <summary>
        /// 把两个对角点规范化为「起点=左下、终点=右上」：X/T 各自取最小值写入本体，最大值写入 End。
        /// 拖动角标结束后调用，保证 End 不会落在 Start 的另一侧；也适用于任何外部写入可能造成的反转。
        /// </summary>
        public void NormalizeCorners()
        {
            var swapX = EndIndicator.XGrid.TotalUnit < XGrid.TotalUnit;
            var minX = swapX ? EndIndicator.XGrid : XGrid;
            var maxX = swapX ? XGrid : EndIndicator.XGrid;

            var swapT = EndIndicator.TGrid.TotalUnit < TGrid.TotalUnit;
            var minT = swapT ? EndIndicator.TGrid : TGrid;
            var maxT = swapT ? TGrid : EndIndicator.TGrid;

            // 先移动本体，保证 End 的 TGrid setter 的 (End >= Start) 约束始终成立。
            XGrid = minX.CopyNew();
            TGrid = minT.CopyNew();
            EndIndicator.XGrid = maxX.CopyNew();
            EndIndicator.TGrid = maxT.CopyNew();
        }

        private IDisplayableObject[] displayables;

        public IndividualSoflanAreaEndIndicator EndIndicator { get; }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => displayables;

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not IndividualSoflanArea from)
                return;

            SoflanGroup = from.SoflanGroup;
            EndIndicator.XGrid = from.EndIndicator.XGrid.CopyNew();
            // 源区域始终是规范化的（End >= Start），本体 TGrid 已由 base.Copy 复制，因此这里的 clamp 不会触发。
            EndIndicator.TGrid = from.EndIndicator.TGrid.CopyNew();
        }

        public IndividualSoflanArea()
        {
            EndIndicator = new IndividualSoflanAreaEndIndicator() { RefIndividualSoflanArea = this };
            EndIndicator.PropertyChanged += EndIndicator_PropertyChanged;
            displayables = [this, EndIndicator];
        }

        public override string ToString() => $"[{IDShortName}] Id({Id}) Group({SoflanGroup}) XGrid({XGrid.TotalUnit.ToString("0.###", CultureInfo.CurrentCulture)}, {EndIndicator.XGrid.TotalUnit.ToString("0.###", CultureInfo.CurrentCulture)}) TGrid({TGrid.TotalUnit.ToString("0.###", CultureInfo.CurrentCulture)}, {EndIndicator.TGrid.TotalUnit.ToString("0.###", CultureInfo.CurrentCulture)})";

        private void EndIndicator_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(e.PropertyName);
        }
    }
}

