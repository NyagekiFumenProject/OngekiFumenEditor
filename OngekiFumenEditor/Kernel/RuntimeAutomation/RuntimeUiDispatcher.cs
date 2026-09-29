using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    internal static class RuntimeUiDispatcher
    {
        /// <summary>
        /// 在 UI 线程上执行并等待结果；已在 UI 线程（或无 WPF Application）时直接执行。
        /// </summary>
        public static Task<T> RunAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                return Task.FromResult(func());

            return dispatcher.InvokeAsync(func, DispatcherPriority.Normal, cancellationToken).Task;
        }

        /// <summary>
        /// 同上，但用于本身是异步的操作（例如打开文档需要等待加载会话）。
        /// </summary>
        public static Task<T> RunAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                return func();

            return dispatcher.InvokeAsync(func, DispatcherPriority.Normal, cancellationToken).Task.Unwrap();
        }
    }
}
