using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public interface IFumenVisualEditorProvider : IEditorProvider
    {
        Task Open(IDocument document,EditorProjectDataModel projModel);
    }
}
