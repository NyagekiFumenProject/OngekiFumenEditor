namespace Gemini.Modules.Output.Views
{
	public interface IOutputView
	{
        bool AutoScrollEnd { get; set; }

		void Clear();
		void ScrollToEnd();
		void AppendText(string text);
		void SetText(string text);
	}
}
