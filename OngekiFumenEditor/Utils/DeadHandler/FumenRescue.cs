using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace OngekiFumenEditor.Utils.DeadHandler
{
	internal static class FumenRescue
	{
		public static async Task<string[]> Rescue()
		{
			var list = new List<string>();
			try
			{
				var editorManager = IoC.Get<IEditorDocumentManager>();
				foreach (var editor in editorManager.GetCurrentEditors())
				{
					var savedFolderPath = await Rescue(editor);
					if (Directory.Exists(savedFolderPath))
					{
						Log.LogInfo($"Rescue fumen/proj file successfully: {savedFolderPath}");
						list.Add(savedFolderPath);
						//return savedFolderPath;
					}
				}
			}
			catch
			{

			}
			return list.ToArray();
		}

		public static async Task<string> Rescue(FumenVisualEditorViewModel editor)
		{
			var projFilePath = editor.FilePath;
			var docName = "NotSavedUnknown-" + RandomHepler.RandomString(10);
			if (!string.IsNullOrWhiteSpace(projFilePath))
				docName = Path.GetFileNameWithoutExtension(projFilePath);

			var rescueFolderPath = TempFileHelper.GetTempFolderPath("Rescue", docName);

			try
			{
				//save proj file
				var extName = ".nyagekiProj";
				if (!string.IsNullOrWhiteSpace(projFilePath))
					extName = Path.GetExtension(projFilePath);
				var tempProjFile = Path.Combine(rescueFolderPath, docName + extName);
				var result = await EditorProjectDataUtils.TrySaveProjFileAsync(tempProjFile, editor.EditorProjectData);
				if (!result.IsSuccess)
					return string.Empty;
			}
			catch
			{
				return string.Empty;
			}

			try
			{
				//save fumen file
				var fumenName = Path.GetFileName(editor.EditorProjectData.FumenFilePath);
				if (!string.IsNullOrWhiteSpace(fumenName))
					fumenName = RandomHepler.RandomString() + ".ogkr";

				var tempFumenFile = Path.Combine(rescueFolderPath, fumenName);
				var result = await EditorProjectDataUtils.TrySaveFumenFileAsync(tempFumenFile, editor.EditorProjectData);
				if (!result.IsSuccess)
					return string.Empty;
			}
			catch
			{
				return string.Empty;
			}

			return rescueFolderPath;
		}
	}
}
