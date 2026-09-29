using Caliburn.Micro;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace OngekiFumenEditor.Utils
{
    public class CommonPropertyChangedBase : PropertyChangedBase
    {
        public static readonly PropertyChangedEventArgs AllProperties = new(string.Empty);

        private static readonly IDictionary<string, PropertyChangedEventArgs> argsCache = new ConcurrentDictionary<string, PropertyChangedEventArgs>();

        public static PropertyChangedEventArgs ArgsOf(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return AllProperties;

            if (argsCache.TryGetValue(propertyName, out var cached))
                return cached;

            return argsCache[propertyName] = new PropertyChangedEventArgs(propertyName);
        }

        public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
            => NotifyOfPropertyChange(ArgsOf(propertyName));

        public virtual void NotifyOfPropertyChange(PropertyChangedEventArgs arg)
        {
            if (!IsNotifying)
                return;

            if (arg is null)
                throw new System.ArgumentNullException(nameof(arg));

            if (PlatformProvider.Current.PropertyChangeNotificationsOnUIThread &&
                Application.Current?.Dispatcher is { } dispatcher &&
                !dispatcher.CheckAccess())
            {
                DispatchToUIThread(arg);
                return;
            }

            OnPropertyChanged(arg);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void DispatchToUIThread(PropertyChangedEventArgs arg) => OnUIThread(() => OnPropertyChanged(arg));
    }
}
