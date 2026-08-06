using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Avalonia.Base;
// using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
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
using DereTore.Exchange.Archive.ACB;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base
{
	public class EditorProjectDataUtils
	{
		private static EditorProjectFileManager projFileManager = new EditorProjectFileManager();

		public record Result(bool IsSuccess, string ErrorMessage);

		public static async Task<EditorProjectDataModel> TryLoadFromFileAsync(
			ISimpleDirectory projectRoot,
			ISimpleFile projectFile,
			string projectFileLocator,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(projectRoot);
			ArgumentNullException.ThrowIfNull(projectFile);
			if (!EditorProjectPathResolver.TryNormalizeRootRelativeLocator(
					projectFileLocator,
					out var normalizedProjectLocator,
					out var projectLocatorError))
			{
				throw new InvalidDataException(projectLocatorError);
			}

			EditorProjectDataModel projectData;
			await using (var projectStream = await projectFile.OpenRead())
				projectData = await projFileManager.Load(projectStream, cancellationToken);

			var defaultFumenLocator = Path.GetFileNameWithoutExtension(projectFile.FileName) + ".ogkr";
			var rawFumenLocator = string.IsNullOrWhiteSpace(projectData.FumenFilePath)
				? defaultFumenLocator
				: projectData.FumenFilePath;

			var errors = new List<string>();
			if (!EditorProjectPathResolver.TryResolveDependency(
					projectRoot,
					normalizedProjectLocator,
					rawFumenLocator,
					out var fumenFile,
					out _,
					out var projectRelativeFumenLocator,
					out var fumenError))
			{
				errors.Add($"Fumen '{rawFumenLocator}': {fumenError}");
			}

			if (!EditorProjectPathResolver.TryResolveDependency(
					projectRoot,
					normalizedProjectLocator,
					projectData.AudioFilePath,
					out var audioFile,
					out var rootRelativeAudioLocator,
					out var projectRelativeAudioLocator,
					out var audioError))
			{
				errors.Add($"Audio '{projectData.AudioFilePath}': {audioError}");
			}

			ISimpleFile audioAwbFile = null;
			if (audioFile is not null &&
				Path.GetExtension(audioFile.FileName).Equals(".acb", StringComparison.OrdinalIgnoreCase))
			{
				if (string.IsNullOrWhiteSpace(audioFile.LocalPath))
				{
					errors.Add($"Audio '{projectData.AudioFilePath}': ACB decoding is not supported on this platform.");
				}
				else
				{
					try
					{
						await using var acbStream = await audioFile.OpenRead();
						using var acb = AcbFile.FromStream(acbStream, audioFile.FileName, disposeStream: false);
						if (acb.InternalAwb is null)
						{
							var rawAwbLocator = acb.ExternalAwb?.FileName;
							if (string.IsNullOrWhiteSpace(rawAwbLocator))
							{
								errors.Add($"Audio '{projectData.AudioFilePath}': the ACB has no usable AWB data.");
							}
							else if (!EditorProjectPathResolver.TryResolveDependency(
								         projectRoot,
								         rootRelativeAudioLocator,
								         rawAwbLocator,
								         out audioAwbFile,
								         out _,
								         out _,
								         out var awbError))
							{
								errors.Add($"Audio AWB '{rawAwbLocator}': {awbError}");
							}
							else if (string.IsNullOrWhiteSpace(audioAwbFile?.LocalPath))
							{
								errors.Add($"Audio AWB '{rawAwbLocator}': external AWB decoding is not supported on this platform.");
							}
						}
					}
					catch (Exception exception)
					{
						errors.Add($"Audio '{projectData.AudioFilePath}': the ACB package cannot be inspected: {exception.Message}");
					}
				}
			}

			if (errors.Count > 0)
				throw new InvalidDataException(string.Join(Environment.NewLine, errors));

			OngekiFumen fumen;
			await using (var fumenStream = await fumenFile!.OpenRead())
			{
				var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(fumenFile.FileName);
				if (fumenDeserializer is null)
					throw new NotSupportedException($"{Lang.DeserializeFumenFileNotSupport}{fumenFile.FileName}");
				fumen = await fumenDeserializer.DeserializeAsync(fumenStream);
			}

			/*
			 * SVG prefab support is temporarily disabled. Keep the parser/formatter compatibility
			 * layer, but do not resolve or read SVG dependencies while opening a project.
			var svgBindings = new List<(SvgImageFilePrefab Svg, ISimpleFile File, string Locator)>();
			foreach (var svg in fumen.SvgPrefabs.OfType<SvgImageFilePrefab>())
			{
				if (!EditorProjectPathResolver.TryResolveRootResource(
						projectRoot,
						svg.SvgFilePath,
						out var svgFile,
						out var svgLocator,
						out var svgError))
				{
					errors.Add($"SVG '{svg.SvgFilePath}' at {svg.TGrid}: {svgError}");
					continue;
				}

				svgBindings.Add((svg, svgFile!, svgLocator));
			}

			if (errors.Count > 0)
			{
				foreach (var svg in fumen.SvgPrefabs)
					svg.Dispose();
				throw new InvalidDataException(string.Join(Environment.NewLine, errors));
			}

			foreach (var binding in svgBindings)
			{
				try
				{
					await binding.Svg.BindProjectFileAsync(
						binding.File,
						binding.Locator,
						cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					foreach (var svg in fumen.SvgPrefabs)
						svg.Dispose();
					throw;
				}
				catch (Exception exception)
				{
					errors.Add(
						$"SVG '{binding.Locator}' at {binding.Svg.TGrid} cannot be loaded: {exception.Message}");
				}
			}

			if (errors.Count > 0)
			{
				foreach (var svg in fumen.SvgPrefabs)
					svg.Dispose();
				throw new InvalidDataException(string.Join(Environment.NewLine, errors));
			}
			*/

			projectData.Fumen = fumen;
			projectData.FumenFilePath = projectRelativeFumenLocator;
			projectData.AudioFilePath = projectRelativeAudioLocator;
			projectData.ProjectFileLocator = normalizedProjectLocator;
			projectData.ProjectFile = projectFile;
			projectData.FumenFile = fumenFile;
			projectData.AudioFile = audioFile;
			projectData.AudioAwbFile = audioAwbFile;
			projectData.ProjectRoot = projectRoot;
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
			ISimpleFile projectFile,
			EditorProjectDataModel editorProject,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(projectFile);
				ArgumentNullException.ThrowIfNull(editorProject);
				if (editorProject.FumenFile is not { } fumenFile)
					throw new InvalidOperationException("The project does not have a bound fumen file.");

				cancellationToken.ThrowIfCancellationRequested();
				var cloneProject = await projFileManager.Clone(editorProject);
				cloneProject.Fumen = editorProject.Fumen;
				StoreBulletPalleteListEditorData(cloneProject);

				var fumenSerializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFile.FileName);
				if (fumenSerializer is null)
					throw new NotSupportedException($"{Lang.SerializeFileNotSupport}{fumenFile.FileName}");
				var fumenBytes = await fumenSerializer.SerializeAsync(cloneProject.Fumen);

				byte[] projectBytes;
				await using (var projectBuffer = new MemoryStream())
				{
					await projFileManager.Save(projectBuffer, cloneProject, cancellationToken);
					projectBytes = projectBuffer.ToArray();
				}

				var originalFumen = await fumenFile.ReadAllBytes();
				var originalProject = await projectFile.ReadAllBytes();
				try
				{
					await WriteBytesAsync(fumenFile, fumenBytes, cancellationToken);
					await WriteBytesAsync(projectFile, projectBytes, cancellationToken);
				}
				catch (Exception saveException)
				{
					var rollbackErrors = new List<Exception> { saveException };
					try
					{
						await WriteBytesAsync(projectFile, originalProject, CancellationToken.None);
					}
					catch (Exception rollbackException)
					{
						rollbackErrors.Add(rollbackException);
					}

					try
					{
						await WriteBytesAsync(fumenFile, originalFumen, CancellationToken.None);
					}
					catch (Exception rollbackException)
					{
						rollbackErrors.Add(rollbackException);
					}

					throw new AggregateException("Project save failed; rollback was attempted.", rollbackErrors);
				}

				return new(true, string.Empty);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception)
			{
				return new(false, $"{Lang.CantSaveProjectTotally}{exception.Message}{Environment.NewLine}{exception.StackTrace}");
			}
		}

		private static Task WriteBytesAsync(
			ISimpleFile file,
			byte[] data,
			CancellationToken cancellationToken)
		{
			return file.WriteAsync(
				(stream, writerCancellationToken) =>
					stream.WriteAsync(data, writerCancellationToken).AsTask(),
				cancellationToken);
		}
	}
}



