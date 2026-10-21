using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace OngekiFumenEditor.UI.KeyBinding.Input
{
    internal static class TextInputFocusHelper
    {
        public static bool IsTextInput(DependencyObject element)
        {
            while (element is not null)
            {
                if (element is TextBoxBase or PasswordBox || element is ComboBox { IsEditable: true })
                    return true;

                element = GetParent(element);
            }

            return false;
        }

        private static DependencyObject GetParent(DependencyObject element)
        {
            if (element is ContentElement content)
                return ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent;

            if (element is Visual or Visual3D)
            {
                var parent = VisualTreeHelper.GetParent(element);
                if (parent is not null)
                    return parent;
            }

            return LogicalTreeHelper.GetParent(element) ?? (element as FrameworkElement)?.TemplatedParent;
        }
    }
}
