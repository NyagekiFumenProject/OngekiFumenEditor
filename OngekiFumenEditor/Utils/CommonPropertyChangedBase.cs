using Caliburn.Micro;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace OngekiFumenEditor.Utils
{
    /// <summary>
    /// 供通知频繁的类型继承：按属性名复用 <see cref="PropertyChangedEventArgs"/>，UI 线程上的通知零分配发布
    /// （热路径实测 120 B/次 → 0 B）；跨线程通知转交基类处理，保留其「无订阅者不派发」短路——缺了它，
    /// 无订阅者的通知也会同步 Invoke 到 UI 线程，与渲染期的 Parallel.ForEach 形成死锁。
    /// 用法：直接继承即可；热点属性可把 <see cref="ArgsOf"/> 的结果缓存成静态字段，再调
    /// <see cref="NotifyOfPropertyChange(PropertyChangedEventArgs)"/> 直接发布，连字典查找都省掉。
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
                // 跨线程派发交给基类：基类里有 PropertyChanged != null 短路，无订阅者的通知（如渲染期每帧新建的
                // XGrid）不会同步 Invoke 到 UI 线程；否则 UI 线程正卡在 ProjectileBatchDrawTargetBase 的
                // Parallel.ForEach 里等 worker 时，worker 的 Invoke 永远等不到 → 死锁。
                base.NotifyOfPropertyChange(arg.PropertyName);
                return;
            }

            OnPropertyChanged(arg);
        }
    }
}
