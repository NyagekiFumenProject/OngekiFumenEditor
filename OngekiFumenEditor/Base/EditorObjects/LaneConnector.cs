using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public abstract class LaneConnector : ConnectorLineBase<ConnectableObjectBase>
    {
        public abstract Brush LineColor { get; }
        public override Type ModelViewType => typeof(LaneConnectorViewModel);
    }

    public class LaneLeftConnector : LaneConnector
    {
        static readonly Brush DefaultColor = new SolidColorBrush(Colors.Red);
        public override Brush LineColor => DefaultColor;
    }

    public class LaneCenterConnector : LaneConnector
    {
        static readonly Brush DefaultColor = new SolidColorBrush(Colors.Green);
        public override Brush LineColor => DefaultColor;
    }

    public class LaneRightConnector : LaneConnector
    {
        static readonly Brush DefaultColor = new SolidColorBrush(Colors.Blue);
        public override Brush LineColor => DefaultColor;
    }

    public class WallLeftConnector : LaneConnector
    {
        static readonly Brush DefaultColor = new SolidColorBrush(Colors.HotPink);
        public override Brush LineColor => DefaultColor;
    }

    public class WallRightConnector : LaneConnector
    {
        static readonly Brush DefaultColor = new SolidColorBrush(Colors.HotPink);
        public override Brush LineColor => DefaultColor;
    }

    public class EnemyLaneConnector : ConnectorLineBase<ConnectableObjectBase>
    {
        public override Type ModelViewType => typeof(EnemyLaneConnectorViewModel);
    }

    public class LaneColorfulConnector : LaneConnector
    {
        private SolidColorBrush lineColor = new SolidColorBrush(Colors.Red);
        public override Brush LineColor => lineColor;

        public override bool Set<T>(ref T oldValue, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (propertyName == nameof(From))
            {
                this.RegisterOrUnregisterPropertyChangeEvent(oldValue as INotifyPropertyChanged, newValue as INotifyPropertyChanged, OnLanePropChanged);
                UpdateLineColor();
            }

            return base.Set(ref oldValue, newValue, propertyName);
        }

        private void OnLanePropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ColorfulLaneStart.ColorId))
                UpdateLineColor();
        }

        private void UpdateLineColor()
        {
            lineColor.Color = ((IColorfulLane)From).ColorId.Color;
            NotifyOfPropertyChange(() => LineColor);
        }
    }
}
