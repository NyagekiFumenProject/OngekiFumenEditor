using System.Windows.Controls;

namespace Gemini.Modules.Output.Views
{
    /// <summary>
    /// Interaction logic for OutputView.xaml
    /// </summary>
    public partial class OutputView : UserControl, IOutputView
    {
        public bool AutoScrollEnd { get; set; } = true;

        public OutputView()
        {
            InitializeComponent();
        }

        public void ScrollToEnd()
        {
            outputText.ScrollToEnd();
        }

        public void Clear()
        {
            outputText.Clear();
        }

        public void AppendText(string text)
        {
            outputText.AppendText(text);
            if (AutoScrollEnd)
                ScrollToEnd();
        }

        public void SetText(string text)
        {
            outputText.Text = text;
            if (AutoScrollEnd)
                ScrollToEnd();
        }
    }
}
