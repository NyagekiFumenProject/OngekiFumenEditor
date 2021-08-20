using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public class FumenVisualEditorViewModel : PersistedDocument
    {
        public struct XGridUnitLineViewModel
        {
            public double X { get; set; }
            public bool IsCenterLine { get; set; }

            public override string ToString() => $"{X:F4} {(IsCenterLine ? "Center" : string.Empty)}";
        }

        private OngekiFumen fumen;
        public double XUnitSize => CanvasWidth / (24 * 2) * UnitCloseSize;
        public double CanvasWidth => VisualDisplayer?.ActualWidth ?? 0;
        public FumenVisualEditorView View { get; private set; }
        public ObservableCollection<XGridUnitLineViewModel> XGridUnitLineLocations { get; } = new ObservableCollection<XGridUnitLineViewModel>();
        public Panel VisualDisplayer => View?.VisualDisplayer;

        public override string DisplayName
        {
            get { return base.DisplayName; }
            set
            {
                base.DisplayName = value;
                if(IoC.Get<WindowTitleHelper>() is WindowTitleHelper title)
                {
                    title.TitleContent = base.DisplayName;
                }
            }
        }

        private bool isPreventXAutoClose;
        public bool IsPreventXAutoClose
        {
            get
            {
                return isPreventXAutoClose;
            }
            set
            {
                isPreventXAutoClose = value;
                NotifyOfPropertyChange(() => IsPreventTimelineAutoClose);
            }
        }

        private bool isPreventTimelineAutoClose;
        public bool IsPreventTimelineAutoClose
        {
            get
            {
                return isPreventTimelineAutoClose;
            }
            set
            {
                isPreventTimelineAutoClose = value;
                NotifyOfPropertyChange(() => IsPreventTimelineAutoClose);
            }
        }

        private double unitCloseSize = 4;
        public double UnitCloseSize
        {
            get
            {
                return unitCloseSize;
            }
            set
            {
                unitCloseSize = value;
                RedrawUnitCloseXLines();
                NotifyOfPropertyChange(() => UnitCloseSize);
            }
        }

        private void RedrawUnitCloseXLines()
        {
            XGridUnitLineLocations.Clear();

            var width = CanvasWidth;
            var unit = XUnitSize;

            for (double totalLength = width / 2 + unit; totalLength < width; totalLength += unit)
            {
                XGridUnitLineLocations.Add(new XGridUnitLineViewModel()
                {
                    X = totalLength,
                    IsCenterLine = false
                });
                XGridUnitLineLocations.Add(new XGridUnitLineViewModel()
                {
                    X = (width / 2) - (totalLength - (width / 2)),
                    IsCenterLine = false
                });
            }
            XGridUnitLineLocations.Add(new XGridUnitLineViewModel()
            {
                X = width / 2,
                IsCenterLine = true
            });
        }

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            var view = v as FumenVisualEditorView;

            View = view;
            RedrawUnitCloseXLines();
        }

        private Task InitalizeVisualData()
        {
            var displayableObjects = fumen.GetAllDisplayableObjects();
            //add all displayable object.
            foreach (var obj in displayableObjects)
            {
                var displayObject = Activator.CreateInstance(obj.ModelViewType) as DisplayObjectViewModelBase;
                if (ViewCreateHelper.CreateView(displayObject) is OngekiObjectViewBase view && obj is IOngekiObject o)
                {
                    view.ViewModel.ReferenceOngekiObject = o;
                    view.ViewModel.EditorViewModel = this;
                    VisualDisplayer.Children.Add(view);
                }
            }
            return Task.CompletedTask;
        }

        protected override async Task DoLoad(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen loading : " + filePath);
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
            using var fileStream = File.OpenRead(filePath);
            fumen = await IoC.Get<IOngekiFumenParser>().ParseAsync(fileStream);
            await InitalizeVisualData();
        }

        protected override async Task DoNew()
        {
            fumen = new OngekiFumen();
            await InitalizeVisualData();
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
        }

        protected override async Task DoSave(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen saving : " + filePath);
            await File.WriteAllTextAsync(filePath, fumen.Serialize());
            Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
        }

        public void OnNewObjectAdd(DisplayObjectViewModelBase viewModel)
        {
            var view = ViewCreateHelper.CreateView(viewModel);
            fumen.AddObject(viewModel.ReferenceOngekiObject);

            VisualDisplayer.Children.Add(view);
            viewModel.EditorViewModel = this;

            Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.Name}");
        }

        public void DeleteSelectedObjects()
        {
            var selectedObject = VisualDisplayer.Children.OfType<OngekiObjectViewBase>().Where(x => x.IsSelected).ToArray();
            foreach (var obj in selectedObject)
            {
                VisualDisplayer.Children.Remove(obj);
                fumen.AddObject(obj.ViewModel?.ReferenceOngekiObject);
            }
            Log.LogInfo($"deleted {selectedObject.Length} objects.");
        }

        public void CopySelectedObjects()
        {

        }

        public void PasteCopiesObjects()
        {

        }

        public void OnKeyDown(ActionExecutionContext e)
        {
            if (e.EventArgs is KeyEventArgs arg)
            {
                if (arg.Key == Key.Delete)
                {
                    DeleteSelectedObjects();
                }
            }
        }
    }
}
