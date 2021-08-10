using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.TextEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.TextEditor
{
    [Export(typeof(IEditorProvider))]
    class TextEditorProvider : IEditorProvider
    {
        public IEnumerable<EditorFileType> FileTypes
        {
            get
            {
                yield return new EditorFileType("Ongeki Fumen File Format", ".ogkr");
            }
        }

        public bool CanCreateNew => true;

        public IDocument Create() => new TextEditorViewModel();

        public bool Handles(string path) => Path.GetExtension(path).Contains(".ogkr");

        public Task New(IDocument document, string name) => ((TextEditorViewModel) document).New(name);
        public Task Open(IDocument document, string path) =>((TextEditorViewModel)document).Load(path);
    }
}
