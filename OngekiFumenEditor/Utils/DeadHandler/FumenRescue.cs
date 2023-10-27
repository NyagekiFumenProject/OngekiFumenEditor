using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.IO;

namespace OngekiFumenEditor.Utils.DeadHandler
{
	internal static class FumenRescue
	{
		public static void Rescue()
		{
			try
			{
				var editorManager = IoC.Get<IEditorDocumentManager>();
				foreach (var editor in editorManager.GetCurrentEditors())
				{
					if (Rescue(editor, out var savedFolderPath))
					{
						Log.LogInfo($"Rescue fumen/proj file successfully: {savedFolderPath}");
					}
				}
			}
			catch
			{

			}
		}

		public static bool Rescue(FumenVisualEditorViewModel editor, out string savedPath)
		{
			var projFilePath = editor.FilePath;
			var docName = "NotSavedUnknown-" + RandomHepler.RandomString(10);
			if (!string.IsNullOrWhiteSpace(projFilePath))
				docName = Path.GetFileNameWithoutExtension(projFilePath);

			var rescueFolderPath = TempFileHelper.GetTempFolderPath("Rescue", docName);
			savedPath = default;

			try
			{
				//save proj file
				var extName = ".nyagekiProj";
				if (!string.IsNullOrWhiteSpace(projFilePath))
					extName = Path.GetExtension(projFilePath);

				var tempProjFile = Path.Combine(rescueFolderPath, docName + extName);
				var result = EditorProjectDataUtils.TrySaveProjFileAsync(tempProjFile, editor.EditorProjectData).Result;
				if (!result.IsSuccess)
					return false;
			}
			catch
			{
				return false;
			}

			try
			{
				//save fumen file
				var fumenName = Path.GetFileName(editor.EditorProjectData.FumenFilePath);
				if (!string.IsNullOrWhiteSpace(fumenName))
					fumenName = RandomHepler.RandomString() + ".ogkr";

				var tempFumenFile = Path.Combine(rescueFolderPath, fumenName);
				var task = EditorProjectDataUtils.TrySaveFumenFileAsync(tempFumenFile, editor.EditorProjectData);
				var result = task.Result;
				if (!result.IsSuccess)
					return false;
			}
			catch
			{
				return false;
			}

			savedPath = rescueFolderPath;
			return true;
		}
	}
}
