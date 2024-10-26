using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace OngekiFumenEditor.Utils
{
    [Export(typeof(WindowTitleHelper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class WindowTitleHelper
    {
        [Import(typeof(IMainWindow))]
        private IMainWindow window = default;

        private string titlePrefix = "Ongeki Fumen Editor";
        public string TitlePrefix
        {
            get => titlePrefix;
            set
            {
                titlePrefix = value;
                UpdateWindowTitle();
            }
        }

        private string titleSuffix = string.Empty;
        public string TitleSuffix
        {
            get => titleSuffix;
            set
            {
                titleSuffix = value;
                UpdateWindowTitle();
            }
        }

        private string titleContent = string.Empty;
        public string TitleContent
        {
            get
            {
                return titleContent;
            }
            set
            {
                titleContent = value;
                UpdateWindowTitle();
            }
        }

        public string ActualFormattedWindowTitle => window.Title;

        public ImageSource Icon
        {
            get
            {
                return window.Icon;
            }
            set
            {
                window.Icon = value;
            }
        }

        public void UpdateWindowTitle()
        {
            var actualTitle = TitlePrefix + TitleContent + TitleSuffix;
            window.Title = actualTitle;
        }

        public void UpdateWindowTitleByEditor(FumenVisualEditorViewModel editor)
        {
            var titleContent = editor != null ? $" - {editor.DisplayName} " : string.Empty;
            TitleContent = titleContent;
        }
    }
}
