using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Utils
{
    internal static class ActionExecutionContextExtensionMethod
    {
        private class DisableHandle : IDisposable
        {
            private FrameworkElement element;

            public DisableHandle(ActionExecutionContext ctx)
            {
                if (ctx.Source is FrameworkElement element)
                {
                    this.element = element;
                    this.element.IsEnabled = false;
                    this.element.IsEnabledChanged += Element_IsEnabledChanged;
                }
            }

            private void Element_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
            {
                //IsEnable has changed at somewhere, so we needn't change it again
                element.IsEnabledChanged -= Element_IsEnabledChanged;
                element = default;
            }

            public void Dispose()
            {
                if (element is null)
                    return;
                element.IsEnabledChanged -= Element_IsEnabledChanged;
                element.IsEnabled = true;
            }
        }

        public static IDisposable DisableSourceByDisposable(this ActionExecutionContext ctx)
        {
            return new DisableHandle(ctx);
        }
    }
}
