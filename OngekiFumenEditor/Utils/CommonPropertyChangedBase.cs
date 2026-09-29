using Caliburn.Micro;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace OngekiFumenEditor.Utils
{
    /// <summary>
    /// 供通知频繁的类型继承：按属性名复用 <see cref="PropertyChangedEventArgs"/>，去掉 Caliburn 每次通知的
    /// 闭包 + 委托 + 参数分配（热路径实测 120 B/次 → 0 B）。
    /// 用法：直接继承即可；热点属性可把 <see cref="ArgsOf"/> 的结果缓存成静态字段，再调
    /// <see cref="NotifyOfPropertyChange(PropertyChangedEventArgs)"/> 直接发布，连字典查找都省掉。
    /// 注意：不要把 args 重载里的派发闭包挪回热路径方法体内（方法体内含捕获型 lambda 会多付 ~32 B/次）。
    /// </summary>
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
