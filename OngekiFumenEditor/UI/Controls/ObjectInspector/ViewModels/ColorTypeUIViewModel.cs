using Caliburn.Micro;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class ColorTypeUIViewModel : CommonUIViewModelBase
    {
        public string ColorText => PropertyInfo.ProxyValue is DrawingColor color
            ? "#" + color.ToArgb().ToString("X8", CultureInfo.InvariantCulture)
            : string.Empty;

        public Color PreviewColor => PropertyInfo.ProxyValue is DrawingColor color
            ? color.ToMediaColor()
            : Colors.Transparent;

        public ColorTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {
        }

        protected override void PropertyInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IObjectPropertyAccessProxy.ProxyValue))
            {
                NotifyOfPropertyChange(() => ColorText);
                NotifyOfPropertyChange(() => PreviewColor);
            }
            else
            {
                base.PropertyInfo_PropertyChanged(sender, e);
            }
        }

        public void OnSelectColor(ActionExecutionContext context)
        {
            if (PropertyInfo.IsReadOnly)
                return;

            var dialog = new CommonColorPicker(
                () => PreviewColor,
                color =>
                {
                    if (!PropertyInfo.IsReadOnly)
                        PropertyInfo.ProxyValue = color.ToDrawingColor();
                },
                Resources.ChangeColor)
            {
                Owner = Window.GetWindow(context.Source) ?? Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };
            dialog.Show();
        }
    }
}
