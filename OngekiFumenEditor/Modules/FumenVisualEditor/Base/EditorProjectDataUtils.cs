using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjManager;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
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
				throw new NotSupportedException($"{Resources.DeserializeFumenFileNotSupport}{projectData.FumenFilePath}");
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

		public static async Task<Result> TrySaveProjFileAsync(string projFileFullPath, EditorProjectDataModel editorProject)
		{
			try
			{
				if (!FileHelper.IsPathWritable(projFileFullPath))
					throw new IOException(Resources.CantWriteProjectFileByIoError);

				var tmpProjFilePath = TempFileHelper.GetTempFilePath("FumenProjFile", Path.GetFileNameWithoutExtension(projFileFullPath), Path.GetExtension(projFileFullPath));
				StoreBulletPalleteListEditorData(editorProject);

				await projFileManager.Save(tmpProjFilePath, editorProject);

				File.Copy(tmpProjFilePath, projFileFullPath, true);

				return new(true, "");
			}
			catch (Exception e)
			{
				var msg = $"{Resources.CantSaveProjectFile}{e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
				//Log.LogError(msg);
				//MessageBox.Show(msg);
			}
		}


		public static async Task<Result> TrySaveFumenFileAsync(string fumenFileFullPath, EditorProjectDataModel editorProject)
		{
			try
			{
				if (!FileHelper.IsPathWritable(fumenFileFullPath))
					throw new IOException(Resources.CantWriteFumenFileByIoError);

				var serializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFileFullPath);
				Log.LogDebug($"serializer = {serializer}");
				if (serializer is null)
					throw new NotSupportedException($"{Resources.SerializeFileNotSupport}{Path.GetFileName(fumenFileFullPath)}");

				var tmpFumenFilePath = TempFileHelper.GetTempFilePath("FumenFile", Path.GetFileNameWithoutExtension(fumenFileFullPath), Path.GetExtension(fumenFileFullPath));
				var fumenBuffer = await serializer.SerializeAsync(editorProject.Fumen);
				using var fs = File.OpenWrite(tmpFumenFilePath);
				fs.Write(fumenBuffer);
				fs.Flush();
				fs.Close();

				File.Copy(tmpFumenFilePath, fumenFileFullPath, true);

				return new(true, "");
			}
			catch (Exception e)
			{
				var msg = $"{Resources.CantSaveFumenProject}{e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
			}
		}

		public static async Task<Result> TrySaveEditorAsync(string projFilePath, EditorProjectDataModel editorProject)
		{
			try
			{
				var tmpProjFilePath = TempFileHelper.GetTempFilePath();

				//clone new project object to modify
				var cloneProj = await projFileManager.Clone(editorProject);
				cloneProj.Fumen = editorProject.Fumen;

				var fileFolder = Path.GetDirectoryName(projFilePath);
				if (Path.IsPathFullyQualified(cloneProj.FumenFilePath))
					cloneProj.FumenFilePath = Path.GetRelativePath(fileFolder, cloneProj.FumenFilePath);
				if (Path.IsPathFullyQualified(cloneProj.AudioFilePath))
					cloneProj.AudioFilePath = Path.GetRelativePath(fileFolder, cloneProj.AudioFilePath);

				//save proj to tmp file
				var saveProjTaskResult = await TrySaveProjFileAsync(tmpProjFilePath, cloneProj);
				if (!saveProjTaskResult.IsSuccess)
					throw new Exception(saveProjTaskResult.ErrorMessage);

				//save fumen to tmp file
				var fumenFullPath = Path.Combine(fileFolder, cloneProj.FumenFilePath ?? GetDefaultFumenFilePathForAutoGenerate(projFilePath));
				var tmpFumenFilePath = TempFileHelper.GetTempFilePath(extension: Path.GetExtension(fumenFullPath));

				var saveFumenTaskResult = await TrySaveFumenFileAsync(tmpFumenFilePath, cloneProj);
				if (!saveFumenTaskResult.IsSuccess)
					throw new Exception(saveFumenTaskResult.ErrorMessage);

				//tmp files cover to real files.
				Log.LogDebug($"Copy tmpProjFilePath '{tmpProjFilePath}' to '{projFilePath}'");
				File.Copy(tmpProjFilePath, projFilePath, true);
				Log.LogDebug($"Copy tmpFumenFilePath '{tmpFumenFilePath}' to '{fumenFullPath}'");
				File.Copy(tmpFumenFilePath, fumenFullPath, true);

				return new(true, "");
			}
			catch (Exception e)
			{
				var msg = $"{Resources.CantSaveProjectTotally}{e.Message}{Environment.NewLine}{e.StackTrace}";
				return new(false, msg);
			}
		}
	}
}
