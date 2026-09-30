using Caliburn.Micro;
using Gemini.Framework;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.EditorLayout;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Gemini.Framework.Commands;
using Microsoft.Xaml.Behaviors;
using System.Collections.Generic;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        private IEditorDocumentManager EditorManager => IoC.Get<IEditorDocumentManager>();

        private EditorProjectDataModel editorProjectData = new EditorProjectDataModel();
        public EditorProjectDataModel EditorProjectData
        {
            get
            {
                return editorProjectData;
            }
            set
            {
                var prevFumen = editorProjectData?.Fumen;
                Set(ref editorProjectData, value);
                RecalculateTotalDurationHeight();

                void setupFumen(OngekiFumen cur, OngekiFumen prev)
                {
                    if (prev is not null)
                    {
                        prev.BpmList.OnChangedEvent -= OnTimeSignatureListChanged;
                        prev.MeterChanges.OnChangedEvent -= OnTimeSignatureListChanged;
                        prev.ObjectModifiedChanged -= OnFumenObjectModifiedChanged;
                    }
                    if (cur is not null)
                    {
                        cur.BpmList.OnChangedEvent += OnTimeSignatureListChanged;
                        cur.MeterChanges.OnChangedEvent += OnTimeSignatureListChanged;
                        cur.ObjectModifiedChanged += OnFumenObjectModifiedChanged;
                    }
                    NotifyOfPropertyChange(() => Fumen);
                }

                setupFumen(editorProjectData?.Fumen, prevFumen);

                if (EditorManager.CurrentActivatedEditor == this)
                    IoC.Get<WindowTitleHelper>().UpdateWindowTitleByEditor(this);
            }
        }

        public IAudioPlayer? AudioPlayer
        {
            get
            {
                return field;
            }
            set
            {
                if (field != value)
                    field?.Dispose();

                Set(ref field, value);
            }
        }

        public event LoadingFinishedEventHandler LoadingFinished;

        private void OnSettingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(EditorGlobalSetting.VerticalDisplayScale):
                    RecalculateTotalDurationHeight();
                    var tGrid = GetCurrentTGrid();
                    ScrollTo(tGrid);
                    break;
                case nameof(EditorGlobalSetting.JudgeLineOffsetY):
                case nameof(EditorGlobalSetting.XOffset):
                    //RecalcViewProjectionMatrix();
                    break;
                case nameof(EditorGlobalSetting.PlayFieldBackgroundColor):
                    playFieldBackgroundColor = EditorGlobalSetting.Default.PlayFieldBackgroundColor.AsARGBToColor().ToVector4();
                    break;
                case nameof(EditorGlobalSetting.EnablePlayFieldDrawing):
                    enablePlayFieldDrawing = EditorGlobalSetting.Default.EnablePlayFieldDrawing;
                    break;
                case nameof(EditorGlobalSetting.HideWallLaneWhenEnablePlayField):
                    hideWallLaneWhenEnablePlayField = EditorGlobalSetting.Default.HideWallLaneWhenEnablePlayField;
                    break;
                case nameof(EditorGlobalSetting.EnableShowPlayerLocation):
                    enableShowPlayerLocation = EditorGlobalSetting.Default.EnableShowPlayerLocation;
                    PlayerLocationRecorder.Clear();
                    break;
                case nameof(EditorGlobalSetting.LimitFPS):
                    UpdateActualRenderInterval();
                    break;
                case nameof(EditorGlobalSetting.XGridUnitSpace):
                case nameof(EditorGlobalSetting.DisplayTimeFormat):
                case nameof(EditorGlobalSetting.BeatSplit):
                case nameof(EditorGlobalSetting.XGridDisplayMaxUnit):
                default:
                    break;
            }
        }

        public OngekiFumen Fumen => EditorProjectData.Fumen;

        private void OnFumenObjectModifiedChanged(OngekiObjectBase sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ISelectableObject.IsSelected):
                case nameof(ConnectableChildObjectBase.IsAnyControlSelecting):
                    break;
                default:
                    IsDirty = true;
                    RecalculateScrollMetrics();
                    break;
            }

        }

        public void RecalculateTotalDurationHeight()
        {
            if (EditorProjectData?.AudioDuration is TimeSpan timeSpan && timeSpan > TimeSpan.Zero)
            {
                TotalDurationHeight = ConvertToY(ConvertAudioTimeToTGrid(timeSpan).TotalUnit, Fumen.SoflansMap.DefaultSoflanList);
            }
            else
            {
                timeSpan = AudioPlayer?.Duration ?? TimeSpan.Zero;
                TotalDurationHeight = ConvertToY(ConvertAudioTimeToTGrid(timeSpan).TotalUnit, Fumen.SoflansMap.DefaultSoflanList);
            }

            RecalculateScrollMetrics();
        }

        public bool EnableDragging => !IsBatchMode || (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) &&
                                                       !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
                                                       !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        private bool isSelectRangeDragging;

        private bool isShowCurveControlAlways = false;
        private bool enableShowPlayerLocation;
        private bool hideWallLaneWhenEnablePlayField;

        public bool HideWallLaneWhenEnablePlayField => hideWallLaneWhenEnablePlayField;

        public bool IsShowCurveControlAlways
        {
            get => isShowCurveControlAlways;
            set
            {
                Set(ref isShowCurveControlAlways, value);
                ToastNotify($"{Resources.ShowCurveControlAlways}{(IsShowCurveControlAlways ? Resources.Enable : Resources.Disable)}");
            }
        }

        public bool IsBatchMode
            => GetView() is DependencyObject obj ? Interaction.GetBehaviors(obj).Contains(BatchModeBehavior) : false;

        public EditorSetting Setting { get; } = new EditorSetting();

        public FumenVisualEditorViewModel() : base()
        {
            //replace owned impl
            UndoRedoManager = new DefaultEditorUndoManager(this);

            EditorGlobalSetting.Default.PropertyChanged += OnSettingPropertyChanged;
            DisplayName = default;

            SelectionArea = new(this);
        }

        #region Document New/Save/Load

        protected override async Task DoNew()
        {
            try
            {
                var dialogViewModel = new EditorProjectSetupDialogViewModel();
                var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);
                if (result != true)
                {
                    Log.LogInfo(Resources.CloseEditorByProjectSetupFail);
                    await TryCloseAsync(false);
                    return;
                }

                var projectData = dialogViewModel.EditorProjectData;

                // 用户看到的是「正在加载：哪个文件」，新工程没有工程名就用所选谱面/音源文件名代替。
                static string ResolveTargetName(EditorProjectDataModel data)
                {
                    if (!string.IsNullOrWhiteSpace(data.FumenFilePath))
                        return Path.GetFileName(data.FumenFilePath);
                    if (!string.IsNullOrWhiteSpace(data.AudioFilePath))
                        return Path.GetFileName(data.AudioFilePath);
                    return Resources.EditorLoadingNewProjectName;
                }

                using var session = EditorLoadingSession.Begin(EditorLoadingStep.Parsing, ResolveTargetName(projectData));
                try
                {
                    if (File.Exists(projectData.FumenFilePath))
                    {
                        using var fumenFileStream = File.OpenRead(projectData.FumenFilePath);
                        var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(projectData.FumenFilePath);
                        if (fumenDeserializer is null)
                            throw new NotSupportedException($"{Resources.DeserializeFumenFileFail}{projectData.FumenFilePath}");
                        var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream, session.CancellationToken);
                        Log.LogInfo($"Fumen file loaded: {projectData.FumenFilePath}");
                        projectData.Fumen = fumen;
                    }
                    await LoadInternalAsync(projectData, session);
                    Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
                }
                catch (OperationCanceledException)
                {
                    await AbortLoadingAsync(session);
                }
                catch (Exception e)
                {
                    await HandleLoadFailureAsync($"{Resources.CantCreateProject}{e.Message}", session);
                }
            }
            catch (Exception e)
            {
                // 设置对话框本身失败（此时还没有 session）。
                var errMsg = $"{Resources.CantCreateProject}{e.Message}";
                Log.LogError(errMsg);
                MessageBox.Show(errMsg);
                await TryCloseAsync(false);
            }
        }

        protected override async Task DoLoad(string filePath)
        {
            using var session = EditorLoadingSession.Begin(EditorLoadingStep.Preparing, Path.GetFileName(filePath));
            try
            {
                using var _ = StatusBarHelper.BeginStatus("Editor project file loading : " + filePath);
                Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
                session.ReportStep(EditorLoadingStep.Parsing);
                var projectData = await EditorProjectDataUtils.TryLoadFromFileAsync(filePath, session.CancellationToken);
                await LoadInternalAsync(projectData, session);
                ToastNotify(Resources.LoadProjectFileAndFumenFile);
                IoC.Get<IEditorRecentFilesManager>().PostRecord(new(filePath, DisplayName, RecentOpenType.NormalDocumentOpen));
            }
            catch (OperationCanceledException)
            {
                await AbortLoadingAsync(session);
            }
            catch (Exception e)
            {
                await HandleLoadFailureAsync($"{Resources.CantLoadProject}{e.Message}", session);
            }
        }

        public async Task Load(EditorProjectDataModel projModel, EditorLoadingSession session)
        {
            try
            {
                await LoadInternalAsync(projModel, session);
            }
            catch (OperationCanceledException)
            {
                await AbortLoadingAsync(session);
            }
            catch (Exception e)
            {
                await HandleLoadFailureAsync($"{Resources.CantLoadProject}{e.Message}", session);
            }
        }

        private async Task HandleLoadFailureAsync(string errorMessage, EditorLoadingSession session)
        {
            Log.LogError(errorMessage);
            MessageBox.Show(errorMessage);
            session.Complete(EditorLoadingOutcome.Failed);
            await TryCloseAsync(false);
        }

        private async Task LoadInternalAsync(EditorProjectDataModel projModel, EditorLoadingSession session)
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            EditorProjectData = projModel;
            session.ReportStep(EditorLoadingStep.LoadingAudio);
            AudioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(projModel.AudioFilePath, session.CancellationToken);
            session.CancellationToken.ThrowIfCancellationRequested();

            var dispTGrid = ConvertAudioTimeToTGrid(projModel.RememberLastDisplayTime);
            ScrollTo(dispTGrid);

            LoadingFinished?.Invoke(this, new(Fumen));

            session.ReportStep(EditorLoadingStep.InitializingRender);
            await WaitForEditorReadyAsync(session.CancellationToken);
            await session.CollectLoadingGarbageAsync();
            session.Complete(EditorLoadingOutcome.Ready);
        }

        private async Task WaitForEditorReadyAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.WhenAll(WaitForRenderInitializationIsDone(), WaitForFirstRenderFrameIsDone())
                    .WaitAsync(EditorLoadingSession.EditorReadyTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                Log.LogWarn($"Editor render did not become ready within {EditorLoadingSession.EditorReadyTimeout.TotalSeconds}s; closing the loading dialog anyway.");
            }
        }

        private async Task AbortLoadingAsync(EditorLoadingSession session)
        {
            Log.LogInfo("Editor loading was cancelled.");
            session.Complete(EditorLoadingOutcome.Cancelled);
            await TryCloseAsync(false);
        }

        protected override async Task DoSave(string filePath)
        {
            using var _ = StatusBarHelper.BeginStatus("Fumen saving : " + filePath);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var newProjFilePath = FileDialogHelper.SaveFile(Resources.SaveNewProjectFile, new[] { (FumenVisualEditorProvider.FILE_EXTENSION_NAME, Resources.FumenProjectFile) });
                if (!string.IsNullOrWhiteSpace(newProjFilePath))
                    await Save(newProjFilePath);
                return;
            }
            Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
            EditorProjectData.RememberLastDisplayTime = ConvertTGridToAudioTime(GetCurrentTGrid());
            if (string.IsNullOrWhiteSpace(EditorProjectData.FumenFilePath))
            {
                //ask fumen file save path before save project.
                var dialog = new SaveFileDialog();

                dialog.Filter = FileDialogHelper.GetSupportFumenFileExtensionFilter();

                if (dialog.ShowDialog() != true)
                {
                    MessageBox.Show(Resources.CancelProjectSaveByFumenSaveFail);
                    return;
                }

                EditorProjectData.FumenFilePath = dialog.FileName;
            }

            var saveTaskResult = await EditorProjectDataUtils.TrySaveEditorAsync(filePath, EditorProjectData);
            if (!saveTaskResult.IsSuccess)
            {
                Log.LogError(saveTaskResult.ErrorMessage);
                MessageBox.Show(saveTaskResult.ErrorMessage);
            }
            else
            {
                DisplayName = default;
                ToastNotify(Resources.SaveProjectFileAndFumenFile);
                IoC.Get<IEditorRecentFilesManager>().PostRecord(new(filePath, DisplayName, RecentOpenType.NormalDocumentOpen));
            }
        }

        private int skipClosePromptOnce;

        /// <summary>
        /// 自动化（MCP）专用：让**下一次**关闭询问直接放行。关闭流程里 Caliburn 的 CloseStrategy
        /// 会先问一次 <see cref="CanCloseAsync"/>，而它默认会弹「是否保存」模态框；自动化调用没有
        /// 人来点这个框，所以由调用方先声明「dirty 策略已由我决定」，把弹框跳过。
        /// 一次性消费：关不掉（例如被别处拦下）之后，用户手动关窗仍会正常询问。
        /// </summary>
        public void RequestCloseWithoutPrompt() => Interlocked.Exchange(ref skipClosePromptOnce, 1);

        public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            // 被 RequestCloseWithoutPrompt() 放行的那一次：不弹框，也不改 dirty 状态。
            if (Interlocked.Exchange(ref skipClosePromptOnce, 0) == 1)
                return true;

            if (!IsDirty)
                return true;

            var result = MessageBox.Show(
                Resources.SaveBeforeClosingPrompt.Format(DisplayName),
                Resources.Warning,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    if (IsNew)
                        return await DoSaveAs(this);
                    await Save(FilePath);
                    return true;
                case MessageBoxResult.No:
                    return true;
                default:
                    return false;
            }
        }

        public class LoadingFinishedEventArgs(OngekiFumen fumen)
        {
            public OngekiFumen Fumen { get; } = fumen;
        }

        public delegate void LoadingFinishedEventHandler(object sender, LoadingFinishedEventArgs args);

        #endregion

        #region Activation

        protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            await base.OnActivatedAsync(cancellationToken);
            await IoC.Get<ISchedulerManager>().AddScheduler(this);
            EditorManager.NotifyActivate(this);
        }

        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            await base.OnDeactivateAsync(close, cancellationToken);
            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
            EditorManager.NotifyDeactivate(this);
            AudioPlayer?.Pause();
            if (close)
                DisposeRenderLoop();
        }

        protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
        {
            await base.OnInitializedAsync(cancellationToken);
            EditorManager.NotifyCreate(this);
        }

        public override async Task TryCloseAsync(bool? dialogResult = null)
        {
            await base.TryCloseAsync(dialogResult);

            AudioPlayer?.Pause();
            AudioPlayer?.Dispose();
            AudioPlayer = null;

            if (dialogResult != false)
                EditorManager.NotifyDestory(this);
        }

        #endregion
    }

    public enum SelectRegionType
    {
        Select,
        SelectFiltered,
        Delete,
        DeleteFiltered
    }
}
