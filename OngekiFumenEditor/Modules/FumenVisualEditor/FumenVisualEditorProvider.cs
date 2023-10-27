using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
	[Export(typeof(IEditorProvider))]
	[Export(typeof(IFumenVisualEditorProvider))]
	class FumenVisualEditorProvider : IFumenVisualEditorProvider
	{
		public const string FILE_EXTENSION_NAME = ".nyagekiProj";
		public static readonly string[] SupportExts = new[]
		{
			FILE_EXTENSION_NAME,
            //".ogkr"
        };

		public IEnumerable<EditorFileType> FileTypes
		{
			get
			{
				yield return new EditorFileType("Fumen Visual Editor Project", FILE_EXTENSION_NAME);
			}
		}

		public bool CanCreateNew => true;

		public IDocument Create() => new FumenVisualEditorViewModel();

		public bool Handles(string path) => SupportExts.Any(ext => Path.GetExtension(path).Equals(ext, StringComparison.OrdinalIgnoreCase));

		public async Task New(IDocument document, string name) => await (document as FumenVisualEditorViewModel)?.New(name);

		public async Task Open(IDocument document, string path) => await (document as FumenVisualEditorViewModel)?.Load(path);

		public async Task Open(IDocument document, EditorProjectDataModel projModel) => await (document as FumenVisualEditorViewModel)?.Load(projModel);
	}
}
