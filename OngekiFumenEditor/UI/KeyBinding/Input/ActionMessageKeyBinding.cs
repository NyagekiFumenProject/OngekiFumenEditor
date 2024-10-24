using Caliburn.Micro;
using Microsoft.Xaml.Behaviors;
using OngekiFumenEditor.Kernel.KeyBinding;
using System;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.UI.KeyBinding.Input
{
    public class ActionMessageKeyBinding : TriggerBase<UIElement>
    {
        private Microsoft.Xaml.Behaviors.TriggerAction generatedMessageAction = default;

        #region DP

        public static readonly DependencyProperty DefinitionProperty =
            DependencyProperty.Register("Definition", typeof(KeyBindingDefinition), typeof(KeyBinding), null);

        public KeyBindingDefinition Definition
        {
            get { return (KeyBindingDefinition)GetValue(DefinitionProperty); }
            set { SetValue(DefinitionProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(KeyBinding), new PropertyMetadata((e, d) =>
            {
                (e as ActionMessageKeyBinding)?.OnMessageChanged(d);
            }));

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty IsEnableProperty =
            DependencyProperty.Register("IsEnable", typeof(bool), typeof(KeyBinding), new PropertyMetadata(true, (e, d) =>
            {
                (e as ActionMessageKeyBinding)?.OnIsEnableChanged(d);
            }));

        public bool IsEnable
        {
            get { return (bool)GetValue(IsEnableProperty); }
            set { SetValue(IsEnableProperty, value); }
        }

        #endregion

        private void OnIsEnableChanged(DependencyPropertyChangedEventArgs d)
        {
            if (generatedMessageAction is Microsoft.Xaml.Behaviors.TriggerAction action)
                action.IsEnabled = IsEnable;
        }

        private void OnMessageChanged(DependencyPropertyChangedEventArgs d)
        {
            RebuildMessageAction();
        }

        private void RebuildMessageAction()
        {
            //remove
            if (generatedMessageAction != null)
            {
                Actions.Remove(generatedMessageAction);
                generatedMessageAction = default;
            }

            //try build again
            if ((!string.IsNullOrWhiteSpace(Message)) && AssociatedObject != null)
            {
                generatedMessageAction = Caliburn.Micro.Parser.CreateMessage(AssociatedObject, Message);
                generatedMessageAction.IsEnabled = IsEnable;
                Actions.Add(generatedMessageAction);
            }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.KeyDown += OnAssociatedObjectKeyDown;
            RebuildMessageAction();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.KeyDown -= OnAssociatedObjectKeyDown;
            RebuildMessageAction();
        }

        private void OnAssociatedObjectKeyDown(object sender, KeyEventArgs e)
        {
            if (Definition is KeyBindingDefinition def && IoC.Get<IKeyBindingManager>().CheckKeyBinding(def, e)) {
                InvokeActions(e);

                if (def.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    // Windows plays an annoying "ding" sound if Alt keys are not handled.
                    e.Handled = true;
                }
            }
        }
    }
}
