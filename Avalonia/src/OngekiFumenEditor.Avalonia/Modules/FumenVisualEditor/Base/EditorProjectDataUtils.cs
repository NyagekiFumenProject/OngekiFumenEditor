﻿using CommunityToolkit.Mvvm.ComponentModel;
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

		public static async Task<EditorContext> TryLoadFromFileAsync(
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

			// The loader consumes the root: success transfers it to the returned context,
			// while every failure path releases the directory tree here.
			EditorProjectDataModel projectData = null;
			EditorContext editorContext = null;
			var rootTransferred = false;
			try
			{
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
				if (OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(audioFile.LocalPath))
				{
					errors.Add($"Audio '{projectData.AudioFilePath}': ACB decoding is not supported on this platform.");
				}
				else
				{
					audioAwbFile = await InspectAcbDependencyAsync(
						projectRoot,
						audioFile,
						rootRelativeAudioLocator,
						projectData.AudioFilePath,
						errors);
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

			projectData.FumenFilePath = projectRelativeFumenLocator;
			projectData.AudioFilePath = projectRelativeAudioLocator;
				editorContext = new EditorContext
				{
					ProjectData = projectData,
					Fumen = fumen,
					ProjectFileLocator = normalizedProjectLocator,
					FileAccessContext = new EditorFileAccessContext
					{
						ProjectDirectory = projectRoot,
						ProjectFile = projectFile,
						FumenFile = fumenFile,
						AudioFile = audioFile,
						AudioAwbFile = audioAwbFile
					}
				};
				rootTransferred = true;
				ApplyBulletPalleteListEditorData(projectData, fumen);
				return editorContext;
			}
			catch
			{
				editorContext?.Dispose();
				if (!rootTransferred)
					projectRoot.Dispose();
				throw;
			}
		}

		// Keep the legacy desktop-only ACB parser out of the general browser load path.
		private static async Task<ISimpleFile> InspectAcbDependencyAsync(
			ISimpleDirectory projectRoot,
			ISimpleFile audioFile,
			string rootRelativeAudioLocator,
			string audioFilePath,
			List<string> errors)
		{
			try
			{
				await using var acbStream = await audioFile.OpenRead();
				using var acb = AcbFile.FromStream(acbStream, audioFile.FileName, disposeStream: false);
				if (acb.InternalAwb is not null)
					return null;

				var rawAwbLocator = acb.ExternalAwb?.FileName;
				if (string.IsNullOrWhiteSpace(rawAwbLocator))
				{
					errors.Add($"Audio '{audioFilePath}': the ACB has no usable AWB data.");
					return null;
				}

				if (!EditorProjectPathResolver.TryResolveDependency(
						projectRoot,
						rootRelativeAudioLocator,
						rawAwbLocator,
						out var audioAwbFile,
						out _,
						out _,
						out var awbError))
				{
					errors.Add($"Audio AWB '{rawAwbLocator}': {awbError}");
					return null;
				}

				if (string.IsNullOrWhiteSpace(audioAwbFile?.LocalPath))
				{
					errors.Add($"Audio AWB '{rawAwbLocator}': external AWB decoding is not supported on this platform.");
					return null;
				}

				return audioAwbFile;
			}
			catch (Exception exception)
			{
				errors.Add($"Audio '{audioFilePath}': the ACB package cannot be inspected: {exception.Message}");
				return null;
			}
		}

		private static void ApplyBulletPalleteListEditorData(EditorProjectDataModel projectData, OngekiFumen fumen)
		{
			foreach (var bpl in fumen.BulletPalleteList)
			{
				if (projectData.StoreBulletPalleteEditorDatas.TryGetValue(bpl.StrID, out var storeEditorData))
				{
					bpl.EditorName = storeEditorData.Name;
					bpl.EditorAxuiliaryLineColor = storeEditorData.AuxiliaryLineColor;
				}
			}
		}

		// Rebuilds a project from a restored file-access context (recent-record snapshot).
		// On success the context ownership transfers to the returned EditorContext; every
		// failure path disposes the context so bookmarked handles never leak.
		public static async Task<EditorContext> TryLoadFromContextAsync(
			EditorFileAccessContext context,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(context);
			context.ThrowIfDisposed();

			EditorProjectDataModel projectData = null;
			EditorContext editorContext = null;
			var contextTransferred = false;
			try
			{
				var projectFile = context.ProjectFile
					?? throw new InvalidDataException("The restored project context has no project descriptor file.");
				var fumenFile = context.FumenFile
					?? throw new InvalidDataException("The restored project context has no fumen file.");
				var audioFile = context.AudioFile
					?? throw new InvalidDataException("The restored project context has no audio file.");

				await using (var projectStream = await projectFile.OpenRead())
					projectData = await projFileManager.Load(projectStream, cancellationToken);

				var errors = new List<string>();

				ISimpleFile audioAwbFile = null;
				if (Path.GetExtension(audioFile.FileName).Equals(".acb", StringComparison.OrdinalIgnoreCase))
				{
					if (OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(audioFile.LocalPath))
					{
						errors.Add($"Audio '{audioFile.FileName}': ACB decoding is not supported on this platform.");
					}
					else
					{
						audioAwbFile = await InspectRestoredAcbDependencyAsync(audioFile, errors);
					}
				}

				if (errors.Count > 0)
					throw new InvalidDataException(string.Join(Environment.NewLine, errors));

				OngekiFumen fumen;
				await using (var fumenStream = await fumenFile.OpenRead())
				{
					var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(fumenFile.FileName);
					if (fumenDeserializer is null)
						throw new NotSupportedException($"{Lang.DeserializeFumenFileNotSupport}{fumenFile.FileName}");
					fumen = await fumenDeserializer.DeserializeAsync(fumenStream);
				}

				editorContext = new EditorContext
				{
					ProjectData = projectData,
					Fumen = fumen
				};
				if (audioAwbFile is not null)
					context.AudioAwbFile = audioAwbFile;
				editorContext.FileAccessContext = context;
				contextTransferred = true;
				ApplyBulletPalleteListEditorData(projectData, fumen);
				return editorContext;
			}
			catch
			{
				editorContext?.Dispose();
				if (!contextTransferred)
					context.Dispose();
				throw;
			}
		}

		// Restore path: the snapshot only bookmarks the ACB, and the restored project root is a
		// shallow wrap, so an external AWB cannot be located. Internal-AWB ACBs restore fine.
		private static async Task<ISimpleFile> InspectRestoredAcbDependencyAsync(
			ISimpleFile audioFile,
			List<string> errors)
		{
			try
			{
				await using var acbStream = await audioFile.OpenRead();
				using var acb = AcbFile.FromStream(acbStream, audioFile.FileName, disposeStream: false);
				if (acb.InternalAwb is not null)
					return null;

				var rawAwbLocator = acb.ExternalAwb?.FileName;
				if (string.IsNullOrWhiteSpace(rawAwbLocator))
				{
					errors.Add($"Audio '{audioFile.FileName}': the ACB has no usable AWB data.");
					return null;
				}

				errors.Add($"Audio '{audioFile.FileName}': the external AWB '{rawAwbLocator}' cannot be restored from a recent-project snapshot.");
				return null;
			}
			catch (Exception exception)
			{
				errors.Add($"Audio '{audioFile.FileName}': the ACB package cannot be inspected: {exception.Message}");
				return null;
			}
		}

		private static void StoreBulletPalleteListEditorData(EditorProjectDataModel projectData, OngekiFumen fumen)
		{
			foreach (var bpl in fumen.BulletPalleteList)
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
			EditorContext editorContext,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(projectFile);
				ArgumentNullException.ThrowIfNull(editorContext);
				StoreBulletPalleteListEditorData(editorContext.ProjectData, editorContext.Fumen);
				await projFileManager.Save(projectFile, editorContext.ProjectData, cancellationToken);
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
			EditorContext editorContext,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(fumenFile);
				ArgumentNullException.ThrowIfNull(editorContext);
				var serializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFile.Name);
				Log.LogDebug($"serializer = {serializer}");
				if (serializer is null)
					throw new NotSupportedException($"{Lang.SerializeFileNotSupport}{fumenFile.Name}");

				var fumenBuffer = await serializer.SerializeAsync(editorContext.Fumen);
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
			EditorContext editorContext,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(fumenFile);
				ArgumentNullException.ThrowIfNull(editorContext);

				var serializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFile.FileName);
				Log.LogDebug($"serializer = {serializer}");
				if (serializer is null)
					throw new NotSupportedException($"{Lang.SerializeFileNotSupport}{fumenFile.FileName}");

				var fumenBuffer = await serializer.SerializeAsync(editorContext.Fumen);
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
			EditorContext editorContext,
			CancellationToken cancellationToken = default)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(projectFile);
				ArgumentNullException.ThrowIfNull(editorContext);
				if (editorContext.FumenFile is not { } fumenFile)
					throw new InvalidOperationException("The project does not have a bound fumen file.");

				cancellationToken.ThrowIfCancellationRequested();
				var cloneProject = await projFileManager.Clone(editorContext.ProjectData);
				StoreBulletPalleteListEditorData(cloneProject, editorContext.Fumen);

				var fumenSerializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFile.FileName);
				if (fumenSerializer is null)
					throw new NotSupportedException($"{Lang.SerializeFileNotSupport}{fumenFile.FileName}");
				var fumenBytes = await fumenSerializer.SerializeAsync(editorContext.Fumen);

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



