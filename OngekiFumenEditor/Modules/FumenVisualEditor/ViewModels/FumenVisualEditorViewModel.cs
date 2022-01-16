using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Threading;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
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
                TotalDurationHeight = value.AudioDuration;
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
                if (IoC.Get<IFumenVisualEditorSettings>() is IFumenVisualEditorSettings editorSettings && IsActive)
                    editorSettings.Setting = value;
                NotifyOfPropertyChange(() => Setting);
            }
        }

        private void OnSettingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(EditorSetting.UnitCloseSize):
                    RecalculateXUnitSize();
                    Redraw(RedrawTarget.XGridUnitLines);
                    break;
                case nameof(EditorSetting.CurrentDisplayTimePosition):
                    ScrollViewerVerticalOffset = TGridCalculator.ConvertTGridToY(Setting.CurrentDisplayTimePosition, this);
                    break;
                case nameof(EditorSetting.BeatSplit):
                case nameof(EditorSetting.BaseLineY):
                    Redraw(RedrawTarget.TGridUnitLines | RedrawTarget.ScrollBar);
                    break;
                case nameof(EditorSetting.EditorDisplayName):
                    if (IoC.Get<WindowTitleHelper>() is WindowTitleHelper title)
                        title.TitleContent = base.DisplayName;
                    break;
                case nameof(EditorSetting.XGridMaxUnit):
                    Redraw(RedrawTarget.OngekiObjects | RedrawTarget.XGridUnitLines);
                    break;
                default:
                    break;
            }
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
                    EditorProjectData.Fumen.BpmList.OnChangedEvent -= OnBPMListChanged;
                if (value is not null)
                    value.BpmList.OnChangedEvent += OnBPMListChanged;
                EditorProjectData.Fumen = value;
                OnFumenObjectLoaded();
                Redraw(RedrawTarget.All);
                NotifyOfPropertyChange(() => Fumen);
            }
        }

        private double canvasWidth = default;
        public double CanvasWidth
        {
            get => canvasWidth;
            set
            {
                Set(ref canvasWidth, value);
                RecalculateXUnitSize();
            }
        }
        private double canvasHeight = default;
        public double CanvasHeight
        {
            get => canvasHeight;
            set
            {
                Set(ref canvasHeight, value);
                RecalculateXUnitSize();
            }
        }

        public ObservableCollection<XGridUnitLineViewModel> XGridUnitLineLocations { get; } = new();
        public ObservableCollection<TGridUnitLineViewModel> TGridUnitLineLocations { get; } = new();
        public ObservableCollection<TGridUnitLineViewModel> _TGridUnitLineLocations { get; } = new();
        public bool isDragging;
        public bool isMouseDown;

        public FumenVisualEditorViewModel()
        {
            Setting = new EditorSetting();
        }

        public ObservableCollection<object> EditorViewModels { get; } = new();

        private void OnFumenObjectLoaded()
        {
            IoC.Get<IFumenMetaInfoBrowser>().Fumen = Fumen;
            IoC.Get<IFumenBulletPalleteListViewer>().Fumen = Fumen;
        }

        #region Document New/Save/Load

        protected override async Task DoNew()
        {
            var dialogViewModel = new EditorProjectSetupDialogViewModel();
            var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);
            if (result != true)
            {
                Log.LogInfo($"用户无法完成新建项目向导，关闭此编辑器");
                await TryCloseAsync(false);
                return;
            }
            EditorProjectData = dialogViewModel.EditorProjectData;
            Redraw(RedrawTarget.All);
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
        }

        protected override async Task DoLoad(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Editor project file loading : " + filePath);
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
            var projectData = await EditorProjectDataUtils.TryLoadFromFileAsync(filePath);
            EditorProjectData = projectData;
            Redraw(RedrawTarget.All);
        }

        protected override async Task DoSave(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen saving : " + filePath);
            Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
            await EditorProjectDataUtils.TrySaveToFileAsync(filePath, EditorProjectData);
        }

        #endregion

        #region Activation

        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (IoC.Get<IFumenVisualEditorSettings>() is IFumenVisualEditorSettings editorSettings)
                editorSettings.Setting = Setting;
            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (IoC.Get<IFumenVisualEditorSettings>() is IFumenVisualEditorSettings editorSettings && editorSettings.Setting == Setting)
                editorSettings.Setting = default;
            return base.OnDeactivateAsync(close, cancellationToken);
        }

        #endregion

        public void AddOngekiObject(DisplayObjectViewModelBase viewModel)
        {
            if (viewModel is IEditorDisplayableViewModel m)
                m.OnObjectCreated(viewModel.ReferenceOngekiObject, this);
            Fumen.AddObject(viewModel.ReferenceOngekiObject);
            EditorViewModels.Add(viewModel);
            //Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.GetType().Name}");
        }
    }
}
