using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base
{
	public class EditorProjectDataUtils
	{
		private static EditorProjectFileManager projFileManager = new EditorProjectFileManager();

		public record Result(bool IsSuccess, string ErrorMessage);

		public static string GetDefaultFumenFilePathForAutoGenerate(string editorProjectFilePath)
			=> Path.Combine(Path.GetDirectoryName(editorProjectFilePath), Path.GetFileNameWithoutExtension(editorProjectFilePath) + ".ogkr");

		public static async Task<EditorProjectDataModel> TryLoadFromFileAsync(string filePath)
		{
			Log.LogDebug($"filePath = {filePath}");
			var projectData = await projFileManager.Load(filePath);

			projectData.FumenFilePath = projectData.FumenFilePath ?? GetDefaultFumenFilePathForAutoGenerate(filePath);

			//always make full path
			var fileFolder = Path.GetDirectoryName(filePath);
			Log.LogDebug($"fileFolder = {fileFolder}");
			projectData.FumenFilePath = Path.Combine(fileFolder, projectData.FumenFilePath);
			Log.LogDebug($"projectData.FumenFilePath = {projectData.FumenFilePath}");
			projectData.AudioFilePath = Path.GetFullPath(Path.Combine(fileFolder, projectData.AudioFilePath));
			Log.LogDebug($"projectData.AudioFilePath = {projectData.AudioFilePath}");

			using var fumenFileStream = File.OpenRead(projectData.FumenFilePath);
			var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(projectData.FumenFilePath);
			Log.LogDebug($"fumenDeserializer = {fumenDeserializer}");
			if (fumenDeserializer is null)
				throw new NotSupportedException($"{Lang.DeserializeFumenFileNotSupport}{projectData.FumenFilePath}");
			var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);
			projectData.Fumen = fumen;

			ApplyBulletPalleteListEditorData(projectData);

			return projectData;
		}

		private static void ApplyBulletPalleteListEditorData(EditorProjectDataModel projectData)
		{
			foreach (var bpl in projectData.Fumen.BulletPalleteList)
			{
				if (projectData.StoreBulletPalleteEditorDatas.TryGetValue(bpl.StrID, out var storeEditorData))
				{
					bpl.EditorName = storeEditorData.Name;
					bpl.EditorAxuiliaryLineColor = storeEditorData.AuxiliaryLineColor;
				}
			}
		}

		private static void StoreBulletPalleteListEditorData(EditorProjectDataModel projectData)
		{
			foreach (var bpl in projectData.Fumen.BulletPalleteList)
			{
				if (projectData.StoreBulletPalleteEditorDatas.TryGetValue(bpl.StrID, out var storeEditorData))
				{
					storeEditorData.Name = bpl.EditorName;
					storeEditorData.AuxiliaryLineColor = bpl.EditorAxuiliaryLineColor;
				}
				else
				{
					projectData.StoreBulletPalleteEditorDatas[bpl.StrID] = new()
					{
						AuxiliaryLineColor = bpl.EditorAxuiliaryLineColor,
						Name = bpl.EditorName
					};
				}
			}
		}

		public static async Task<Result> TrySaveProjFileAsync(
			string projFileFullPath,
			EditorProjectDataModel editorProject,
			CancellationToken cancellationToken = default)
		{
			try
			{
				if (!FileHelper.IsPathWritable(projFileFullPath))
					throw new IOException(Lang.CantWriteProjectFileByIoError);

				var tmpProjFile = await CreateTemporaryFileAsync(
					"FumenProjFile",
					"project",
					Path.GetExtension(projFileFullPath),
					cancellationToken);
				StoreBulletPalleteListEditorData(editorProject);

				await projFileManager.Save(tmpProjFile, editorProject, cancellationToken);

				File.Copy(tmpProjFile.GetRequiredLocalPath(), projFileFullPath, true);

				return new(true, "");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				var msg = $"{Lang.CantSaveProjectFile}{e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
				//Log.LogError(msg);
				//MessageBox.Show(msg);
			}
		}

		public static async Task<Result> TrySaveProjFileAsync(
			ITemporaryFile projectFile,
			EditorProjectDataModel editorProject,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(projectFile);
				StoreBulletPalleteListEditorData(editorProject);
				await projFileManager.Save(projectFile, editorProject, cancellationToken);
				return new(true, "");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				var msg = $"{Lang.CantSaveProjectFile}{e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
			}
		}


		public static async Task<Result> TrySaveFumenFileAsync(
			string fumenFileFullPath,
			EditorProjectDataModel editorProject,
			CancellationToken cancellationToken = default)
		{
			try
			{
				if (!FileHelper.IsPathWritable(fumenFileFullPath))
					throw new IOException(Lang.CantWriteFumenFileByIoError);

				var serializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFileFullPath);
				Log.LogDebug($"serializer = {serializer}");
				if (serializer is null)
					throw new NotSupportedException($"{Lang.SerializeFileNotSupport}{Path.GetFileName(fumenFileFullPath)}");

				var tmpFumenFile = await CreateTemporaryFileAsync(
					"FumenFile",
					"fumen",
					Path.GetExtension(fumenFileFullPath),
					cancellationToken);
				var fumenBuffer = await serializer.SerializeAsync(editorProject.Fumen);
				await tmpFumenFile.WriteAllBytesAsync(fumenBuffer, cancellationToken);

				File.Copy(tmpFumenFile.GetRequiredLocalPath(), fumenFileFullPath, true);

				return new(true, "");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				var msg = $"{Lang.CantSaveFumenProject} {e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
			}
		}

		public static async Task<Result> TrySaveFumenFileAsync(
			ITemporaryFile fumenFile,
			EditorProjectDataModel editorProject,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(fumenFile);
				var serializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFile.Name);
				Log.LogDebug($"serializer = {serializer}");
				if (serializer is null)
					throw new NotSupportedException($"{Lang.SerializeFileNotSupport}{fumenFile.Name}");

				var fumenBuffer = await serializer.SerializeAsync(editorProject.Fumen);
				await fumenFile.WriteAllBytesAsync(fumenBuffer, cancellationToken);
				return new(true, "");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				var msg = $"{Lang.CantSaveFumenProject} {e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
			}
		}

		public static async Task<Result> TrySaveFumenFileAsync(
			ISimpleFile fumenFile,
			EditorProjectDataModel editorProject,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(fumenFile);

				var serializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFile.FileName);
				Log.LogDebug($"serializer = {serializer}");
				if (serializer is null)
					throw new NotSupportedException($"{Lang.SerializeFileNotSupport}{fumenFile.FileName}");

				var fumenBuffer = await serializer.SerializeAsync(editorProject.Fumen);
				await fumenFile.WriteAsync(
					(stream, writerCancellationToken) =>
						stream.WriteAsync(fumenBuffer, writerCancellationToken).AsTask(),
					cancellationToken);

				return new(true, "");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				var msg = $"{Lang.CantSaveFumenProject} {e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
			}
		}

		public static async Task<Result> TrySaveEditorAsync(
			string projFilePath,
			EditorProjectDataModel editorProject,
			CancellationToken cancellationToken = default)
		{
			try
			{
				var tmpProjFile = await CreateTemporaryFileAsync(
					"misc",
					"project",
					".dat",
					cancellationToken);
				var tmpProjFilePath = tmpProjFile.GetRequiredLocalPath();

				//clone new project object to modify
				cancellationToken.ThrowIfCancellationRequested();
				var cloneProj = await projFileManager.Clone(editorProject);
				cloneProj.Fumen = editorProject.Fumen;

				var fileFolder = Path.GetDirectoryName(projFilePath);
				if (Path.IsPathFullyQualified(cloneProj.FumenFilePath))
					cloneProj.FumenFilePath = Path.GetRelativePath(fileFolder, cloneProj.FumenFilePath);
				if (Path.IsPathFullyQualified(cloneProj.AudioFilePath))
					cloneProj.AudioFilePath = Path.GetRelativePath(fileFolder, cloneProj.AudioFilePath);

				//save proj to tmp file
				var saveProjTaskResult = await TrySaveProjFileAsync(tmpProjFile, cloneProj, cancellationToken);
				if (!saveProjTaskResult.IsSuccess)
					throw new Exception(saveProjTaskResult.ErrorMessage);

				string fumenFullPath = null;
				string tmpFumenFilePath = null;
				Result saveFumenTaskResult;
				if (editorProject.FumenFile is null)
				{
					// Keep the local-path branch atomic by serializing to a temporary file first.
					fumenFullPath = Path.Combine(fileFolder, cloneProj.FumenFilePath ?? GetDefaultFumenFilePathForAutoGenerate(projFilePath));
					var tmpFumenFile = await CreateTemporaryFileAsync(
						"misc",
						"fumen",
						Path.GetExtension(fumenFullPath),
						cancellationToken);
					tmpFumenFilePath = tmpFumenFile.GetRequiredLocalPath();
					saveFumenTaskResult = await TrySaveFumenFileAsync(tmpFumenFile, cloneProj, cancellationToken);
				}
				else
				{
					saveFumenTaskResult = await TrySaveFumenFileAsync(
						editorProject.FumenFile,
						cloneProj,
						cancellationToken);
				}

				if (!saveFumenTaskResult.IsSuccess)
					throw new Exception(saveFumenTaskResult.ErrorMessage);

				//tmp files cover to real files.
				Log.LogDebug($"Copy tmpProjFilePath '{tmpProjFilePath}' to '{projFilePath}'");
				File.Copy(tmpProjFilePath, projFilePath, true);
				if (tmpFumenFilePath is not null)
				{
					Log.LogDebug($"Copy tmpFumenFilePath '{tmpFumenFilePath}' to '{fumenFullPath}'");
					File.Copy(tmpFumenFilePath, fumenFullPath, true);
				}

				return new(true, "");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				var msg = $"{Lang.CantSaveProjectTotally}{e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
			}
		}

		private static async Task<ITemporaryFile> CreateTemporaryFileAsync(
			string folderName,
			string prefix,
			string extension,
			CancellationToken cancellationToken)
		{
			var provider = IoC.Get<ITemporaryFolderProvider>();
			var folder = await provider.Root.GetOrCreateFolderAsync(folderName, cancellationToken);
			return await provider.CreateUniqueFileAsync(
				prefix,
				extension ?? string.Empty,
				folder,
				cancellationToken);
		}
	}
}



