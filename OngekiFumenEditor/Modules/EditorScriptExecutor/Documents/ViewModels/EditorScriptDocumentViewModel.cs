using Caliburn.Micro;
using Gemini.Framework;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Documents.Views;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Documents.ViewModels
{
	[Export(typeof(EditorScriptDocumentViewModel))]
	[MapToView(ViewType = typeof(EditorScriptDocumentView))]
	public class EditorScriptDocumentViewModel : PersistedDocument
	{
		public class EditorItem
		{
			public string Name => TargetEditor?.DisplayName ?? Resources.NoEditorTarget;
			public FumenVisualEditorViewModel TargetEditor { get; set; }
		}

		private static EditorItem DefaultEmpty { get; } = new EditorItem { };

		public ICSharpCode.AvalonEdit.Document.TextDocument ScriptDocument { get; set; } = new();
		public ObservableCollection<EditorItem> CurrentEditors { get; } = new();

		private EditorItem currentSelectedEditor = default;

		CompletionWindow completionWindow = default;
		private IDocumentContext documentContext;

		public EditorItem CurrentSelectedEditor
		{
			get => currentSelectedEditor;
			set
			{
				Set(ref currentSelectedEditor, value);
				NotifyOfPropertyChange(() => IsEnableRun);
			}
		}

		public bool IsEnableRun => CurrentSelectedEditor is not null;

		private FileSystemWatcher watcher;
		private string watchingCsFilePath;
		private string currentProjFilePath;

		public FileSystemWatcher Watcher
		{
			get => watcher;
			set
			{
				if (watcher is not null)
				{
					watcher.EnableRaisingEvents = false;
					watcher.Renamed -= SyncCSFileToScriptTextDocument;
					Watcher.Changed -= Watcher_Changed;
					watcher.Error -= Watcher_Error;
				}

				Set(ref watcher, value);

				if (Watcher is not null)
				{
					Watcher.Renamed += SyncCSFileToScriptTextDocument;
					Watcher.Changed += Watcher_Changed;
					Watcher.Error += Watcher_Error;
					Watcher.EnableRaisingEvents = true;
				}

				NotifyOfPropertyChange(() => IsUsingWatcher);
			}
		}

		private void Watcher_Changed(object sender, FileSystemEventArgs e)
		{
			if (e.Name == Path.GetFileName(watchingCsFilePath))
				SyncCsFileToScriptContent();
		}

		public bool IsUsingWatcher => watchingCsFilePath is not null;

		private void Watcher_Error(object sender, ErrorEventArgs e)
		{
			Log.LogError($"CSFile watcher throw error.");
			Watcher = null;
		}

		public void SyncCsFileToScriptContent()
		{
			if (!File.Exists(watchingCsFilePath))
				return;

			(GetView() as DispatcherObject)?.Dispatcher.Invoke(async () =>
			{
				try
				{
					ScriptDocument.Text = await File.ReadAllTextAsync(watchingCsFilePath);
					await DoSave(FilePath);
					Log.LogInfo($"sync .cs file content to script : {FileName} ({DisplayName})");
				}
				catch
				{

				}
			});
		}

		private void SyncCSFileToScriptTextDocument(object sender, RenamedEventArgs e)
		{
			if (e.Name == Path.GetFileName(watchingCsFilePath))
				SyncCsFileToScriptContent();
		}

		public async void Init()
		{
			documentContext?.Dispose();
			documentContext = await IoC.Get<IEditorScriptExecutor>().InitDocumentContext();

			var editorManager = IoC.Get<IEditorDocumentManager>();
			editorManager.OnNotifyCreated += UpdateCurrentEditorList;
			editorManager.OnNotifyDestoryed += UpdateCurrentEditorList;
			UpdateCurrentEditorList();
		}

		private void UpdateCurrentEditorList(FumenVisualEditorViewModel _ = default)
		{
			var r = CurrentSelectedEditor;
			CurrentEditors.Clear();
			CurrentEditors.Add(DefaultEmpty);
			foreach (var editor in IoC.Get<IEditorDocumentManager>().GetCurrentEditors())
				CurrentEditors.Add(new EditorItem() { TargetEditor = editor });
			if (!CurrentEditors.Contains(CurrentSelectedEditor))
				CurrentSelectedEditor = default;
			if (r == DefaultEmpty)
				CurrentSelectedEditor = DefaultEmpty;
		}

		protected override async Task DoLoad(string filePath)
		{
			ScriptDocument.Text = await File.ReadAllTextAsync(filePath);
			Init();
			IoC.Get<IEditorRecentFilesManager>().PostRecord(new(filePath, DisplayName, RecentOpenType.NormalDocumentOpen));
		}

		protected override async Task DoNew()
		{
			Init();
			try
			{
				var asmDirPath = Path.GetDirectoryName(typeof(EditorScriptDocumentViewModel).Assembly.Location);
				var templateFilePath = Path.Combine(asmDirPath, "Resources", "NewScriptTemplate.nyagekiScript");
				if (File.Exists(templateFilePath))
				{
					ScriptDocument.Text = await File.ReadAllTextAsync(templateFilePath);
				}
			}
			catch (Exception e)
			{
				Log.LogDebug($"{Resources.UseTemplateScriptFileFail}{e.Message}");
			}
		}

		protected override async Task DoSave(string filePath)
		{
			try
			{
				using var _ = StatusBarHelper.BeginStatus("Fumen saving : " + filePath);
				if (string.IsNullOrWhiteSpace(filePath))
				{
					await DoSaveAs(this);
					return;
				}
				await File.WriteAllTextAsync(filePath, ScriptDocument.Text);
				IsDirty = false;
				IoC.Get<IEditorRecentFilesManager>().PostRecord(new(filePath, DisplayName, RecentOpenType.NormalDocumentOpen));
			}
			catch (Exception e)
			{
				MessageBox.Show($"{Resources.CantSaveScriptFile} {e.Message}");
			}
		}

		public void OnTextChanged()
		{
			IsDirty = true;
		}

		private BuildParam GetBuildParam()
		{
			var buildParam = documentContext.CreateBuildParam();

			buildParam.Script = ScriptDocument.Text;
			buildParam.DisplayFileName = FileName;

			return buildParam;
		}

		public async void OnCheckButtonClicked()
		{
			using var _ = StatusBarHelper.BeginStatus("Script is building ...");
			var buildResult = await IoC.Get<IEditorScriptExecutor>().Build(GetBuildParam());

			if (buildResult.IsSuccess)
			{
				MessageBox.Show(Resources.CompileSuccess);
				return;
			}

			var errorMsg = string.Join(Environment.NewLine, buildResult.Errors);
			MessageBox.Show($"{Resources.CompileError}\n{errorMsg}");
		}

		public async void OnRunButtonClicked()
		{
			using var _ = StatusBarHelper.BeginStatus("Script is building ...");
			var buildResult = await IoC.Get<IEditorScriptExecutor>().Build(GetBuildParam());

			if (!buildResult.IsSuccess)
			{
				var errorMsg = string.Join(Environment.NewLine, buildResult.Errors);
				MessageBox.Show($"{Resources.CompileError}\n{errorMsg}");
				return;
			}

			if (MessageBox.Show(Resources.ComfirmExecuteScript, default, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
				return;

			using var _2 = StatusBarHelper.BeginStatus("Script is executing ...");
			var executeResult = await IoC.Get<IEditorScriptExecutor>().Execute(buildResult, CurrentSelectedEditor?.TargetEditor);

			MessageBox.Show($"{Resources.Execute}{(executeResult.Success ? Resources.Success : $"{Resources.FailedAndReason}{executeResult.ErrorMessage}")}");
		}

		public async void OnReloadFileButtonClicked()
		{
			if (File.Exists(FilePath))
				await DoLoad(FilePath);
		}

		public async void OnVSEditButtonClicked()
		{
			if (IsUsingWatcher && File.Exists(currentProjFilePath))
			{
				Process.Start(new ProcessStartInfo(currentProjFilePath)
				{
					UseShellExecute = true
				});

				return;
			}

			var scriptName = Path.GetFileNameWithoutExtension(FilePath) ?? RandomHepler.RandomString(6);
			var projOutputDirPath = TempFileHelper.GetTempFolderPath("ScriptTempProjects", scriptName);
			var csFileName = $"Script.{scriptName}.cs";
			var csFilePath = Path.Combine(projOutputDirPath, csFileName);

			Log.LogDebug($"projOutputDirPath = {projOutputDirPath}");
			Log.LogDebug($"csFileName = {csFileName}");
			Log.LogDebug($"csFilePath = {csFilePath}");

			Directory.CreateDirectory(projOutputDirPath);
			await File.WriteAllTextAsync(csFilePath, ScriptDocument.Text, Encoding.UTF8);

			if (!documentContext.GenerateProjectFile(projOutputDirPath, csFilePath, out var projFilePath))
			{
				MessageBox.Show(Resources.GenerateScriptProjectFileFail);
				return;
			}

			Log.LogDebug($"projFilePath = {projFilePath}");

			try
			{
				//watch new file
				var watcher = new FileSystemWatcher(Path.GetDirectoryName(csFilePath));
				watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
				watchingCsFilePath = csFilePath;
				currentProjFilePath = projFilePath;
				watcher.Filter = "*.cs";
				Watcher = watcher;

				Process.Start(new ProcessStartInfo(projFilePath)
				{
					UseShellExecute = true
				});
			}
			catch
			{

			}
		}

		internal async void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
		{
			if (e.Text == "." || e.Text == " ")
			{
				var textArea = sender as TextArea;

				completionWindow = new CompletionWindow(textArea);
				IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;

				await foreach (var comp in documentContext.CompleteCode(ScriptDocument.Text, textArea.Caret.Offset, true))
				{
					data.Add(new DefaultCompletionDataModel(comp));
				}

				completionWindow.Closed += delegate
				{
					completionWindow = null;
				};

				completionWindow.Show();
			}
		}

		internal void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
		{
			if (e.Text.Length > 0 && completionWindow != null)
			{
				if (!char.IsLetterOrDigit(e.Text[0]))
				{
					// Whenever a non-letter is typed while the completion window is open,
					// insert the currently selected element.
					completionWindow.CompletionList.RequestInsertion(e);
				}
			}
		}

		public void OnStopCsFileButtonClicked()
		{
			//sync last time.
			SyncCsFileToScriptContent();

			currentProjFilePath = null;
			watchingCsFilePath = null;
			Watcher = null;
		}

		public void OnSpaceKeyDown(ActionExecutionContext e)
		{
			var itor = PresentationSource.CurrentSources.GetEnumerator();
			if (itor.MoveNext())
			{
				/*
                (e.View as UIElement).RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, itor.Current as PresentationSource, 0, Key.Space)
                {
                    RoutedEvent = UIElement.KeyDownEvent
                });
                */
			}
		}
	}
}
