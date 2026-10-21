using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Utils;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public static class SoflanGroupSelector
    {
        public static UIElement Create(IObjectPropertyAccessProxy wrapper) =>
            ViewHelper.CreateViewByViewModelType(() => new SoflanGroupTypeUIViewModel(wrapper));
    }
}
