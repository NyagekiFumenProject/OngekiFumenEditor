using OngekiFumenEditor.Core.Base.Attributes;
using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Base.OngekiObjects
{
    public class IndividualSoflanArea : OngekiMovableObjectBase
    {
        public class IndividualSoflanAreaEndIndicator : OngekiMovableObjectBase
        {
            public override string IDShortName => "[ISF_End]";

            public IndividualSoflanArea RefIndividualSoflanArea { get; internal protected set; }

            public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => System.Array.Empty<IDisplayableObject>();

            private bool tGridHasSet;

            public override TGrid TGrid
            {
                get
                {
                    if (!tGridHasSet)
                    {
                        TGrid = RefIndividualSoflanArea.TGrid.CopyNew();
                        return TGrid;
                    }
                    return base.TGrid;
                }
                set
                {
                    base.TGrid = value is not null && value < RefIndividualSoflanArea.TGrid ? RefIndividualSoflanArea.TGrid.CopyNew() : value;
                    tGridHasSet = true;
                }
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

        private IDisplayableObject[] displayables;

        public IndividualSoflanAreaEndIndicator EndIndicator { get; }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => displayables;

        public IndividualSoflanArea()
        {
            EndIndicator = new IndividualSoflanAreaEndIndicator() { RefIndividualSoflanArea = this };
            EndIndicator.PropertyChanged += EndIndicator_PropertyChanged;
            displayables = [this, EndIndicator];
        }

        public override string ToString() => $"[{IDShortName}] Id({Id}) Group({SoflanGroup}) XGrid({XGrid.TotalUnit}, {EndIndicator.XGrid.TotalUnit}) TGrid({TGrid.TotalUnit}, {EndIndicator.TGrid.TotalUnit})";

        private void EndIndicator_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(e.PropertyName);
        }
    }
}

