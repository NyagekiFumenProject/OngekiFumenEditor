using Caliburn.Micro;
using MahApps.Metro.Controls;
using OngekiFumenEditor.Kernel.KeyBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Xv2CoreLib.PtrWriter;

namespace OngekiFumenEditor.Kernel.SettingPages.KeyBinding.Dialogs
{
    /// <summary>
    /// ConfigKeyBindingDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigKeyBindingDialog : MetroWindow, INotifyPropertyChanged
    {
        public KeyBindingDefinition Definition { get; }

        private ModifierKeys modifier;
        private Key key;

        public event PropertyChangedEventHandler PropertyChanged;

        public string CurrentExpression => KeyBindingDefinition.FormatToExpression(key, modifier);

        public KeyBindingDefinition ConflictDefinition { get; private set; }

        public ConfigKeyBindingDialog(KeyBindingDefinition definition)
        {
            Definition = definition;
            key = definition.Key;
            modifier = definition.Modifiers;

            InitializeComponent();

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (!IsActive)
                return;

            var key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (Definition.Key == Key.None) {
                TryClearModifier(key);
            }

            UpdateExpression();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsActive)
                return;

            var key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (TryGetModifier(key, out var modifier)) {
                this.modifier = modifier;
                this.key = Key.None;
            }
            else
                this.key = key;

            UpdateExpression();
        }

        private bool TryGetModifier(Key key, out ModifierKeys modifier)
        {
            switch (key)
            {
                case Key.LeftCtrl or Key.RightCtrl:
                    modifier = ModifierKeys.Control;
                    return true;
                case Key.LeftShift or Key.RightShift:
                    modifier = ModifierKeys.Shift;
                    return true;
                case Key.LeftAlt or Key.RightAlt:
                    modifier = ModifierKeys.Alt;
                    return true;
                case Key.LWin or Key.RWin:
                    modifier = ModifierKeys.Windows;
                    return true;
                default:
                    modifier = ModifierKeys.None;
                    return false;
            }
        }

        private bool TryClearModifier(Key key)
        {
            switch (key)
            {
                case Key.LeftCtrl or Key.RightCtrl:
                case Key.LeftShift or Key.RightShift:
                case Key.LeftAlt or Key.RightAlt:
                case Key.LWin or Key.RWin:
                    modifier = ModifierKeys.None;
                    return true;
            }

            return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //重新设置
            key = Key.None;
            modifier = ModifierKeys.None;

            UpdateExpression();
        }

        private void UpdateExpression()
        {
            PropertyChanged?.Invoke(this, new(nameof(CurrentExpression)));

            if (!string.IsNullOrWhiteSpace(CurrentExpression))
            {
                ConflictDefinition = IoC.Get<IKeyBindingManager>().QueryKeyBinding(key, modifier, Definition.Layer);
                if (ConflictDefinition == Definition)
                    ConflictDefinition = default;
                PropertyChanged?.Invoke(this, new(nameof(ConflictDefinition)));
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (ConflictDefinition is not null)
            {
                if (MessageBox.Show($"你绑定的键位和 {ConflictDefinition.DisplayName} 冲突, 如果继续绑定则清空对方冲突的键位, 是否继续?", "警告", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;
            }
            UpdateExpression();
            DialogResult = true;
            Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            key = Definition.DefaultKey;
            modifier = Definition.DefaultModifiers;
            UpdateExpression();
        }
    }
}