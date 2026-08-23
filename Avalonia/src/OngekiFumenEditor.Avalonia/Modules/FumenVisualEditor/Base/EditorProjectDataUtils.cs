﻿using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Avalonia.Base;
// using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
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

		// Reads and parses a complete context without consuming its file capabilities. Creation
		// transactions use this boundary so they can remove newly-created files before disposal.
		public static async Task<LoadedEditorProjectData> LoadDataAsync(
			EditorFileAccessContext context,
			CancellationToken cancellationToken = default,
			IFumenParserManager parserManager = null)
		{
			ArgumentNullException.ThrowIfNull(context);
			context.ThrowIfDisposed();

			EditorProjectDataModel projectData = null;
			LoadedEditorProjectData loadedData = null;
			try
			{
				var projectFile = context.ProjectFile
					?? throw new InvalidDataException("The project context has no project descriptor file.");
				var fumenFile = context.FumenFile
					?? throw new InvalidDataException("The project context has no fumen file.");
				var audioFile = context.AudioFile
					?? throw new InvalidDataException("The project context has no audio file.");

				await using (var projectStream = await projectFile.OpenRead())
					projectData = await projFileManager.Load(projectStream, cancellationToken);

				var errors = new List<string>();

				if (Path.GetExtension(audioFile.FileName).Equals(".acb", StringComparison.OrdinalIgnoreCase))
				{
					if (OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(audioFile.LocalPath))
					{
						errors.Add($"Audio '{audioFile.FileName}': ACB decoding is not supported on this platform.");
					}
					else
					{
						await ValidateAcbDependencyAsync(audioFile, context.AudioAwbFile, errors);
					}
				}

				if (errors.Count > 0)
					throw new InvalidDataException(string.Join(Environment.NewLine, errors));

				OngekiFumen fumen;
				await using (var fumenStream = await fumenFile.OpenRead())
				{
					var fumenDeserializer = (parserManager ?? IoC.Get<IFumenParserManager>())
						.GetDeserializer(fumenFile.FileName);
					if (fumenDeserializer is null)
						throw new NotSupportedException($"{Lang.DeserializeFumenFileNotSupport}{fumenFile.FileName}");
					fumen = await fumenDeserializer.DeserializeAsync(fumenStream);
				}

				ApplyBulletPalleteListEditorData(projectData, fumen);
				loadedData = new LoadedEditorProjectData(projectData, fumen);
				return loadedData;
			}
			catch
			{
				loadedData?.Dispose();
				throw;
			}
		}

		// Compatibility wrapper retaining the historical consuming contract: a successful
		// load transfers the context to EditorContext, while every failure disposes it.
		public static async Task<EditorContext> TryLoadFromContextAsync(
			EditorFileAccessContext context,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(context);
			var contextTransferred = false;
			try
			{
				using var loadedData = await LoadDataAsync(context, cancellationToken);
				var (projectData, fumen) = loadedData.Take();
				var editorContext = new EditorContext
				{
					ProjectData = projectData,
					Fumen = fumen,
					FileAccessContext = context
				};
				contextTransferred = true;
				return editorContext;
			}
			finally
			{
				if (!contextTransferred)
					context.Dispose();
			}
		}

		private static async Task ValidateAcbDependencyAsync(
			ISimpleFile audioFile,
			ISimpleFile audioAwbFile,
			List<string> errors)
		{
			try
			{
				await using var acbStream = await audioFile.OpenRead();
				using var acb = AcbFile.FromStream(acbStream, audioFile.FileName, disposeStream: false);
				if (acb.InternalAwb is not null)
					return;

				var expectedAwbFileName = acb.ExternalAwb?.FileName;
				if (string.IsNullOrWhiteSpace(expectedAwbFileName))
				{
					errors.Add($"Audio '{audioFile.FileName}': the ACB has no usable AWB data.");
					return;
				}

				if (audioAwbFile is null)
				{
					errors.Add($"Audio '{audioFile.FileName}': the external AWB '{expectedAwbFileName}' is not bound.");
					return;
				}

				if (string.IsNullOrWhiteSpace(audioAwbFile.LocalPath))
				{
					errors.Add($"Audio AWB '{audioAwbFile.FileName}': external AWB decoding is not supported on this platform.");
				}
			}
			catch (Exception exception)
			{
				errors.Add($"Audio '{audioFile.FileName}': the ACB package cannot be inspected: {exception.Message}");
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
			ISimpleFile projectFile,
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
