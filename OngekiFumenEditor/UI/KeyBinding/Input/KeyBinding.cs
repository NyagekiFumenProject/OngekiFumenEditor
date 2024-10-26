using Caliburn.Micro;
using Microsoft.Xaml.Behaviors;
using OngekiFumenEditor.Kernel.KeyBinding;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.UI.KeyBinding.Input
{
    public class KeyBinding : TriggerBase<UIElement>
    {
        public static readonly DependencyProperty DefinitionProperty =
            DependencyProperty.Register("Definition", typeof(KeyBindingDefinition), typeof(KeyBinding), null);

        public KeyBindingDefinition Definition
        {
            get { return (KeyBindingDefinition)GetValue(DefinitionProperty); }
            set { SetValue(DefinitionProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.KeyDown += OnAssociatedObjectKeyDown;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.KeyDown -= OnAssociatedObjectKeyDown;
        }

        private void OnAssociatedObjectKeyDown(object sender, KeyEventArgs e)
        {
            if (Definition is KeyBindingDefinition def && IoC.Get<IKeyBindingManager>().CheckKeyBinding(def, e))
                InvokeActions(e);
        }
    }
}
