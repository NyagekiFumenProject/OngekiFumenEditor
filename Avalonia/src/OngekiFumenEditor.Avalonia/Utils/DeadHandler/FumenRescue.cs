using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Utils.DeadHandler
{
	internal static class FumenRescue
	{
		public static async Task<string[]> Rescue(CancellationToken cancellationToken = default)
		{
			var list = new List<string>();
			try
			{
				var editorManager = IoC.Get<IEditorDocumentManager>();
				foreach (var editor in editorManager.GetCurrentEditors())
				{
					var savedFolder = await Rescue(editor, cancellationToken);
					if (savedFolder is not null)
					{
						var savedFolderPath = savedFolder.LocalPath ?? savedFolder.RelativePath;
						Log.LogInfo($"Rescue fumen/proj file successfully: {savedFolderPath}");
						list.Add(savedFolderPath);
						//return savedFolderPath;
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch
			{

			}
			return list.ToArray();
		}

		public static async Task<ITemporaryFolder> Rescue(
			FumenVisualEditorViewModel editor,
			CancellationToken cancellationToken = default)
		{
			var projFilePath = editor.FilePath;
			var docName = "NotSavedUnknown-" + RandomHepler.RandomString(10);
			if (!string.IsNullOrWhiteSpace(projFilePath))
				docName = Path.GetFileNameWithoutExtension(projFilePath);

			var provider = IoC.Get<ITemporaryFolderProvider>();
			var rescueRoot = await provider.Root.GetOrCreateFolderAsync("Rescue", cancellationToken);
			var rescueFolder = await provider.CreateUniqueFolderAsync(
				GetSafeRescueFolderPrefix(docName),
				rescueRoot,
				cancellationToken);

			try
			{
				//save proj file
				var extName = ".nyagekiProj";
				if (!string.IsNullOrWhiteSpace(projFilePath))
					extName = Path.GetExtension(projFilePath);
				var tempProjFile = await rescueFolder.GetOrCreateFileAsync(
					$"project{extName}",
					cancellationToken);
				var result = await EditorProjectDataUtils.TrySaveProjFileAsync(
					tempProjFile,
					editor.EditorProjectData,
					cancellationToken);
				if (!result.IsSuccess)
					return null;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch
			{
				return null;
			}

			try
			{
				//save fumen file
				var fumenName = Path.GetFileName(editor.EditorProjectData.FumenFilePath);
				if (string.IsNullOrWhiteSpace(fumenName))
					fumenName = RandomHepler.RandomString() + ".ogkr";
				var fumenExtension = Path.GetExtension(fumenName);
				if (string.IsNullOrWhiteSpace(fumenExtension))
					fumenExtension = ".ogkr";

				var tempFumenFile = await rescueFolder.GetOrCreateFileAsync(
					$"fumen{fumenExtension}",
					cancellationToken);
				var result = await EditorProjectDataUtils.TrySaveFumenFileAsync(
					tempFumenFile,
					editor.EditorProjectData,
					cancellationToken);
				if (!result.IsSuccess)
					return null;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch
			{
				return null;
			}

			return rescueFolder;
		}

		private static string GetSafeRescueFolderPrefix(string documentName)
		{
			const string invalidCharacters = "<>:\"/\\|?*";
			var safeName = new string(documentName
				.Take(100)
				.Select(character =>
					char.IsControl(character) ||
					char.IsWhiteSpace(character) ||
					invalidCharacters.Contains(character)
						? '_'
						: character)
				.ToArray());
			return $"rescue-{safeName}";
		}
	}
}
