using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace OngekiFumenEditor.Avalonia.Utils.DeadHandler
{
	internal static class FumenRescue
	{
		private const string AutoSaveFolderName = "AutoSave";

		public static async Task<ITemporaryFolder> SaveRecoverySnapshotAsync(
			FumenVisualEditorViewModel editor,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(editor);
			if (editor.EditorContext.ProjectData is null || editor.EditorContext is null)
				return null;

			var provider = IoC.Get<ITemporaryFolderProvider>();
			if (!provider.IsAvailable)
				return null;

			var autoSaveRoot = await provider.Root.GetOrCreateFolderAsync(
				AutoSaveFolderName,
				cancellationToken);
			var snapshotFolder = await autoSaveRoot.GetOrCreateFolderAsync(
				editor.RecoverySnapshotId.ToString("N"),
				cancellationToken);

			try
			{
				var projectFile = await snapshotFolder.GetOrCreateFileAsync(
					"project.nyagekiProj",
					cancellationToken);
				var projectResult = await EditorProjectDataUtils.TrySaveProjFileAsync(
					projectFile,
					editor.EditorContext,
					cancellationToken);
				if (!projectResult.IsSuccess)
					throw new IOException(projectResult.ErrorMessage);

				var fumenExtension = Path.GetExtension(editor.EditorContext.FumenFile?.FileName);
				if (string.IsNullOrWhiteSpace(fumenExtension))
					fumenExtension = ".ogkr";
				var fumenFile = await snapshotFolder.GetOrCreateFileAsync(
					$"fumen{fumenExtension}",
					cancellationToken);
				var fumenResult = await EditorProjectDataUtils.TrySaveFumenFileAsync(
					fumenFile,
					editor.EditorContext,
					cancellationToken);
				if (!fumenResult.IsSuccess)
					throw new IOException(fumenResult.ErrorMessage);

				var locator = editor.EditorContext.ProjectFileLocator ?? string.Empty;
				var metadata = string.Join(
					Environment.NewLine,
					"Version=1",
					$"CreatedUtc={DateTimeOffset.UtcNow:O}",
					$"ProjectFileLocatorBase64={Convert.ToBase64String(Encoding.UTF8.GetBytes(locator))}");
				var metadataFile = await snapshotFolder.GetOrCreateFileAsync(
					"metadata.txt",
					cancellationToken);
				await metadataFile.WriteAllBytesAsync(Encoding.UTF8.GetBytes(metadata), cancellationToken);
				return snapshotFolder;
			}
			catch
			{
				try
				{
					await snapshotFolder.DeleteAsync(CancellationToken.None);
				}
				catch
				{
					// Preserve the snapshot creation error.
				}

				throw;
			}
		}

		public static async Task DeleteRecoverySnapshotAsync(
			FumenVisualEditorViewModel editor,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(editor);
			var provider = IoC.Get<ITemporaryFolderProvider>();
			if (!provider.IsAvailable)
				return;

			var autoSaveRoot = await provider.Root.TryGetFolderAsync(
				AutoSaveFolderName,
				cancellationToken);
			var snapshotFolder = autoSaveRoot is null
				? null
				: await autoSaveRoot.TryGetFolderAsync(
					editor.RecoverySnapshotId.ToString("N"),
					cancellationToken);
			if (snapshotFolder is not null)
				await snapshotFolder.DeleteAsync(cancellationToken);
		}

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
			var projFilePath = editor.EditorContext.FilePath;
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
					editor.EditorContext,
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
				var fumenName = Path.GetFileName(editor.EditorContext.ProjectData.FumenFilePath);
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
					editor.EditorContext,
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
