using Caliburn.Micro;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils
{
    public static class PropertyChangedBaseExtensionMethod
    {
        private static readonly ConcurrentDictionary<int, WeakReference<PropertyChangedEventHandler>> savedMethod = new ConcurrentDictionary<int, WeakReference<PropertyChangedEventHandler>>();

        public static void RegisterOrUnregisterPropertyChangeEvent<T>(this PropertyChangedBase t, T oldValue, T newValue, PropertyChangedEventHandler handler) where T : INotifyPropertyChanged
        {
            if (oldValue is not null)
                oldValue.PropertyChanged -= handler;
            if (newValue is not null)
                newValue.PropertyChanged += handler;
        }

        public static void RegisterOrUnregisterPropertyChangeEvent<T>(this PropertyChangedBase t, T oldValue, T newValue) where T : INotifyPropertyChanged
        {
            var oldHash = RuntimeHelpers.GetHashCode(oldValue);
            if (oldValue is not null && savedMethod.TryGetValue(oldHash, out var d))
            {
                if (d.TryGetTarget(out var e))
                    oldValue.PropertyChanged -= e;
                savedMethod.TryRemove(oldHash, out _);
            }

            if (newValue is not null)
            {
                var newHash = RuntimeHelpers.GetHashCode(newValue);
                var w = new PropertyChangedEventHandler((a, b) => t.NotifyOfPropertyChange(b.PropertyName));
                newValue.PropertyChanged += w;
                savedMethod[newHash] = new WeakReference<PropertyChangedEventHandler>(w);
            }
        }
    }
}
