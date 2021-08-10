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
        public IEnumerable<EditorFileType> FileTypes
        {
            get
            {
                yield return new EditorFileType("Ongeki Fumen Visual Editor", ".ogkr_fve");
            }
        }

        public bool CanCreateNew => true;

        public IDocument Create() => new FumenVisualEditorViewModel();

        public bool Handles(string path) => Path.GetExtension(path)?.Contains(".ogkr_fve") ?? false;

        public async Task New(IDocument document, string name) => await (document as FumenVisualEditorViewModel)?.New(name);

        public async Task Open(IDocument document, string path) => await (document as FumenVisualEditorViewModel)?.Load(path);
    }
}
