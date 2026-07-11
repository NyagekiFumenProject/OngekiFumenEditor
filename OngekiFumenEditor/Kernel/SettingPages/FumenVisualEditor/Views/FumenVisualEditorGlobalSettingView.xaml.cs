using System.Windows.Controls;
using System.Windows.Input;
using OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.Views
{
    /// <summary>
    /// FumenVisualEditorGlobalSettingView.xaml 的交互逻辑
    /// </summary>
    public partial class FumenVisualEditorGlobalSettingView : UserControl
    {
        public FumenVisualEditorGlobalSettingView()
        {
            InitializeComponent();
        }

        private void HoldBodyWidthTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitHoldBodyWidth();
        }

        private void HoldBodyWidthTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                CommitHoldBodyWidth();
        }

        private void CommitHoldBodyWidth()
        {
            if (DataContext is FumenVisualEditorGlobalSettingViewModel viewModel)
                viewModel.CommitHoldBodyWidth();
        }
    }
}
