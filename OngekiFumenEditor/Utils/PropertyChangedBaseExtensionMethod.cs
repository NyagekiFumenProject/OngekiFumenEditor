using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class PropertyChangedBaseExtensionMethod
    {
        static Dictionary<int, WeakReference<PropertyChangedEventHandler>> savedMethod = new ();

        public static void RegisterOrUnregisterPropertyChangeEvent<T>(this PropertyChangedBase t, T oldValue, T newValue) where T : INotifyPropertyChanged
        {
            if (oldValue is not null && savedMethod.TryGetValue(oldValue.GetHashCode(), out var d))
            {
                if (d.TryGetTarget(out var e))
                    oldValue.PropertyChanged -= e;
                savedMethod.Remove(oldValue.GetHashCode());
            }

            if (newValue is not null)
            {
                var w = new PropertyChangedEventHandler((a, b) => t.NotifyOfPropertyChange(() => newValue));
                newValue.PropertyChanged += w;
                var q = new WeakReference<PropertyChangedEventHandler>(w);
                savedMethod[newValue.GetHashCode()] = q;
            }
        }
    }
}
