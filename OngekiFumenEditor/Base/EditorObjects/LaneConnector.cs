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
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public abstract class LaneConnector : ConnectorLineBase<ConnectableObjectBase>
    {
        public abstract Brush LineColor { get; }
        public override Type ModelViewType => typeof(LaneConnectorViewModel);
    }

    public class LaneLeftConnector : LaneConnector
    {
        public override Brush LineColor => Brushes.Red;
    }

    public class LaneCenterConnector : LaneConnector
    {
        public override Brush LineColor => Brushes.Green;
    }

    public class LaneRightConnector : LaneConnector
    {
        public override Brush LineColor => Brushes.Blue;
    }

    public class WallLeftConnector : LaneConnector
    {
        public static Brush DefaultBrush { get; } = new SolidColorBrush(Color.FromArgb(255, 181, 156, 231));
        public override Brush LineColor => DefaultBrush;
    }

    public class WallRightConnector : LaneConnector
    {
        public static Brush DefaultBrush { get; } = new SolidColorBrush(Color.FromArgb(255, 231, 149, 178));
        public override Brush LineColor => DefaultBrush;
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
            lineColor.Color = ((From as IColorfulLane)?.ColorId ?? ColorIdConst.Akari).Color;
            NotifyOfPropertyChange(() => LineColor);
        }
    }
}
