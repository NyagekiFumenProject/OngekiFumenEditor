using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    [Export(typeof(IEditorProvider))]
    class FumenVisualEditorProvider : IEditorProvider
    {
        public const string FILE_EXTENSION_NAME = ".nyagekiProj";

        public IEnumerable<EditorFileType> FileTypes
        {
            get
            {
                yield return new EditorFileType("Fumen Visual Editor Project", FILE_EXTENSION_NAME);
            }
        }

        public bool CanCreateNew => true;

        public IDocument Create() => new FumenVisualEditorViewModel();

        public bool Handles(string path) => Path.GetExtension(path).Equals(FILE_EXTENSION_NAME, StringComparison.OrdinalIgnoreCase);

        public async Task New(IDocument document, string name) => await (document as FumenVisualEditorViewModel)?.New(name);

        public async Task Open(IDocument document, string path) => await (document as FumenVisualEditorViewModel)?.Load(path);
    }
}
