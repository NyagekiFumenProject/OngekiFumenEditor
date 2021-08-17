using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        public FumenVisualEditorView View { get; private set; }
        public Panel VisualDisplayer => View?.VisualDisplayer;

        public override string DisplayName
        {
            get { return base.DisplayName; }
            set
            {
                base.DisplayName = value;
                IoC.Get<WindowTitleHelper>().TitleContent = base.DisplayName;
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

        public double CanvasWidth => VisualDisplayer?.ActualWidth ?? 0;

        private int unitCloseSize = 4;
        public int UnitCloseSize
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

        public ObservableCollection<XGridUnitLineViewModel> XGridUnitLineLocations { get; } = new ObservableCollection<XGridUnitLineViewModel>();

        private void RedrawUnitCloseXLines()
        {
            XGridUnitLineLocations.Clear();

            var width = CanvasWidth;
            var unit = width / (2 * UnitCloseSize);

            for (double totalLength = width / 2; totalLength < width; totalLength += unit)
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
        }

        private Task InitalizeVisualData()
        {
            return Task.CompletedTask;
        }

        protected override async Task DoLoad(string filePath)
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad()");
            await InitalizeVisualData();
        }

        protected override async Task DoNew()
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
            await InitalizeVisualData();
        }

        protected override async Task DoSave(string filePath)
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoSave()");
            await InitalizeVisualData();
        }

        public void OnNewObjectAdd(OngekiObjectViewModelBase viewModel)
        {
            var view = ViewCreateHelper.CreateView(viewModel);
            VisualDisplayer.Children.Add(view);
            viewModel.EditorViewModel = this;

            Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.Name}");
        }
    }
}
