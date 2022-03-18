using NAudio.SoundFont;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Controls
{
    public class ObjectPoolItemsControl : ItemsControl
    {
        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
            Log.LogDebug($"{element} {item}");
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            Log.LogDebug($"{element} {item}");
        }
    }
}
