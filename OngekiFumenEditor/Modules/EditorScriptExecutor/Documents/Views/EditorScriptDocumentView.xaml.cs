using OngekiFumenEditor.Modules.EditorScriptExecutor.Documents.ViewModels;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Documents.Views
{
	/// <summary>
	/// EditorScriptDocumentView.xaml 的交互逻辑
	/// </summary>
	public partial class EditorScriptDocumentView : UserControl
	{
		public EditorScriptDocumentView()
		{
			InitializeComponent();

			textEditor.TextArea.TextEntering += (s, e) => (DataContext as EditorScriptDocumentViewModel)?.TextArea_TextEntering(s, e);
			textEditor.TextArea.TextEntered += (s, e) => (DataContext as EditorScriptDocumentViewModel)?.TextArea_TextEntered(s, e);
		}
	}
}
