using AngleSharp.Css.Dom;
using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Modules.Shell.Commands;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
                Set(ref editorProjectData, value);
                TotalDurationHeight = value.AudioDuration.TotalMilliseconds;
                Setting = EditorProjectData.EditorSetting;
                Fumen = EditorProjectData.Fumen;
            }
        }

        public EditorSetting Setting
        {
            get
            {
                return EditorProjectData.EditorSetting;
            }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(EditorProjectData.EditorSetting, value, OnSettingPropertyChanged);
                EditorProjectData.EditorSetting = value;
                NotifyOfPropertyChange(() => Setting);
            }
        }

        private void OnSettingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(EditorSetting.JudgeLineOffsetY):
                    NotifyOfPropertyChange(() => MinVisibleCanvasY);
                    NotifyOfPropertyChange(() => MaxVisibleCanvasY);
                    break;
                case nameof(EditorSetting.XGridUnitSpace):
                    ClearDisplayingObjectCache();
                    Redraw(RedrawTarget.XGridUnitLines);
                    break;
                case nameof(EditorSetting.BeatSplit):
                    //case nameof(EditorSetting.BaseLineY):
                    Redraw(RedrawTarget.TGridUnitLines | RedrawTarget.ScrollBar);
                    break;
                case nameof(EditorSetting.EditorDisplayName):
                    if (IoC.Get<WindowTitleHelper>() is WindowTitleHelper title)
                        title.TitleContent = base.DisplayName;
                    break;
                case nameof(EditorSetting.XGridDisplayMaxUnit):
                    ClearDisplayingObjectCache();
                    Redraw(RedrawTarget.XGridUnitLines);
                    break;
                default:
                    break;
            }
        }

        private void ClearDisplayingObjectCache()
        {
            CurrentDisplayEditorViewModels.Clear();
            EditorViewModels.Clear();
            Redraw(RedrawTarget.OngekiObjects);
        }

        public OngekiFumen Fumen
        {
            get
            {
                return EditorProjectData.Fumen;
            }
            set
            {
                if (EditorProjectData.Fumen is not null)
                {
                    EditorProjectData.Fumen.BpmList.OnChangedEvent -= OnTimeSignatureListChanged;
                    EditorProjectData.Fumen.MeterChanges.OnChangedEvent -= OnTimeSignatureListChanged;
                    EditorProjectData.Fumen.ObjectModifiedChanged -= OnFumenObjectModifiedChanged;
                }
                if (value is not null)
                {
                    value.BpmList.OnChangedEvent += OnTimeSignatureListChanged;
                    value.MeterChanges.OnChangedEvent += OnTimeSignatureListChanged;
                    value.ObjectModifiedChanged += OnFumenObjectModifiedChanged;
                }
                EditorProjectData.Fumen = value;
                Redraw(RedrawTarget.All);
                NotifyOfPropertyChange(() => Fumen);
            }
        }

        private void OnFumenObjectModifiedChanged(OngekiObjectBase sender, PropertyChangedEventArgs e)
        {
            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            var curBrowserObj = objBrowser.OngekiObject;

            switch (e.PropertyName)
            {
                case nameof(ISelectableObject.IsSelected):
                    if (sender is ISelectableObject selectable)
                    {
                        if (!selectable.IsSelected)
                        {
                            if (curBrowserObj == sender)
                                objBrowser.SetCurrentOngekiObject(null, this);
                            CurrentSelectedObjects.Remove(selectable);
                        }
                        else
                        {
                            CurrentSelectedObjects.Add(selectable);
                        }
                        NotifyOfPropertyChange(() => SelectObjects);
                    }
                    break;
                case nameof(ConnectableChildObjectBase.IsAnyControlSelecting):
                    foreach (var controlPoint in ((ConnectableChildObjectBase)sender).PathControls)
                    {
                        var contains = CurrentSelectedObjects.Contains(controlPoint);
                        if (controlPoint.IsSelected && !contains)
                            CurrentSelectedObjects.Add(controlPoint);
                        else if((!controlPoint.IsSelected) && contains)
                            CurrentSelectedObjects.Remove(controlPoint);
                    }
                    break;
                default:
                    IsDirty = true;
                    break;
            }

        }

        public double CalculateYFromAudioTime(TimeSpan audioTime)
        {
            return TotalDurationHeight - audioTime.TotalMilliseconds - CanvasHeight;
        }

        private double canvasWidth = default;
        public double CanvasWidth
        {
            get => canvasWidth;
            set
            {
                Set(ref canvasWidth, value);
            }
        }
        private double canvasHeight = default;
        public double CanvasHeight
        {
            get => canvasHeight;
            set
            {
                Set(ref canvasHeight, value);
            }
        }

        public ObservableCollection<XGridUnitLineViewModel> XGridUnitLineLocations { get; } = new();
        public ObservableCollection<TGridUnitLineViewModel> TGridUnitLineLocations { get; } = new();
        public HashSet<IEditorDisplayableViewModel> EditorViewModels { get; } = new();
        public ObservableCollection<ISelectableObject> CurrentSelectedObjects { get; } = new();

        private bool isDragging;
        private bool isMouseDown;

        private bool brushMode = false;
        public bool BrushMode
        {
            get => brushMode;
            set
            {
                Set(ref brushMode, value);
                ToastNotify($"笔刷模式:{(BrushMode?"开启":"关闭")}");
            }
        }

        public FumenVisualEditorViewModel() : base()
        {
            var editorSetting = Properties.EditorGlobalSetting.Default;
            UndoRedoManager.UndoCountLimit = editorSetting.IsEnableUndoActionSavingLimit ? editorSetting.UndoActionSavingLimit : null;
            Log.LogDebug($"UndoRedoManager.UndoCountLimit: {UndoRedoManager.UndoCountLimit}");
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
                    Log.LogInfo($"用户无法完成新建项目向导，关闭此编辑器");
                    await TryCloseAsync(false);
                    return;
                }
                var projectData = dialogViewModel.EditorProjectData;
                if (File.Exists(projectData.FumenFilePath))
                {
                    using var fumenFileStream = File.OpenRead(projectData.FumenFilePath);
                    var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(projectData.FumenFilePath);
                    if (fumenDeserializer is null)
                        throw new NotSupportedException($"不支持此谱面文件的解析:{projectData.FumenFilePath}");
                    var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);
                    projectData.Fumen = fumen;
                }
                EditorProjectData = dialogViewModel.EditorProjectData;
                Redraw(RedrawTarget.All);
                Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
                await Dispatcher.Yield();
            }
            catch (Exception e)
            {
                var errMsg = $"无法新建项目:{e.Message}";
                Log.LogError(errMsg);
                MessageBox.Show(errMsg);
                await TryCloseAsync(false);
            }
        }

        protected override async Task DoLoad(string filePath)
        {
            try
            {
                using var _ = StatusBarHelper.BeginStatus("Editor project file loading : " + filePath);
                Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
                var projectData = await EditorProjectDataUtils.TryLoadFromFileAsync(filePath);
                await Load(projectData);
                ToastNotify("谱面项目和文件加载成功");
            }
            catch (Exception e)
            {
                var errMsg = $"无法加载项目:{e.Message}";
                Log.LogError(errMsg);
                MessageBox.Show(errMsg);
                await TryCloseAsync(false);
            }
        }

        public Task Load(EditorProjectDataModel projModel)
        {
            EditorProjectData = projModel;
            Redraw(RedrawTarget.All);
            return Task.CompletedTask;
        }

        protected override async Task DoSave(string filePath)
        {
            using var _ = StatusBarHelper.BeginStatus("Fumen saving : " + filePath);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                await DoSaveAs(this);
                return;
            }
            Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
            if (string.IsNullOrWhiteSpace(EditorProjectData.FumenFilePath))
            {
                //ask fumen file save path before save project.
                var dialog = new SaveFileDialog();

                dialog.Filter = FileDialogHelper.GetSupportFumenFileExtensionFilter();

                if (dialog.ShowDialog() != true)
                {
                    MessageBox.Show("无法保存谱面,项目保存取消");
                    return;
                }

                EditorProjectData.FumenFilePath = dialog.FileName;
            }

            ToastNotify("谱面项目和文件保存成功");
            await EditorProjectDataUtils.TrySaveToFileAsync(filePath, EditorProjectData);
        }

        #endregion

        #region Activation

        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            EditorManager.NotifyActivate(this);
        }

        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            await base.OnDeactivateAsync(close, cancellationToken);
            EditorManager.NotifyDeactivate(this);
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            await base.OnInitializeAsync(cancellationToken);
            EditorManager.NotifyCreate(this);
        }

        public override async Task TryCloseAsync(bool? dialogResult = null)
        {
            await base.TryCloseAsync(dialogResult);
            if (dialogResult != false)
                EditorManager.NotifyDestory(this);
        }

        #endregion

        public void AddObject(DisplayObjectViewModelBase viewModel)
        {
            if (viewModel is IEditorDisplayableViewModel m)
                m.OnObjectCreated(viewModel.ReferenceOngekiObject, this);
            Fumen.AddObject(viewModel.ReferenceOngekiObject);
            EditorViewModels.Add(viewModel);
            //Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.GetType().Name}");
        }
    }
}
