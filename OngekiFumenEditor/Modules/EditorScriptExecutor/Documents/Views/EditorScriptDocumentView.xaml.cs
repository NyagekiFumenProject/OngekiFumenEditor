using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Documents.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Navigation;

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
