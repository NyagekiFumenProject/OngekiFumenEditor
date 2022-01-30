using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer.ViewModels
{
    [Export(typeof(ITGridCalculatorToolViewer))]
    public class TGridCalculatorToolViewerViewModel : Tool, ITGridCalculatorToolViewer
    {
        private FumenVisualEditorViewModel editor;
        public FumenVisualEditorViewModel Editor
        {
            get => editor;
            set
            {
                Set(ref editor, value);
                NotifyOfPropertyChange(() => IsEnabled);
            }
        }

        private TGrid tGrid = new();
        public TGrid TGrid
        {
            get => tGrid;
            set => Set(ref tGrid, value);
        }

        private double msec = 0;
        public double Msec
        {
            get => msec;
            set => Set(ref msec, value);
        }

        public bool IsEnabled => Editor is not null;

        public override PaneLocation PreferredLocation => PaneLocation.Right;

        public TGridCalculatorToolViewerViewModel()
        {
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
        }

        public void UpdateToTGrid()
        {
            TGrid = TGridCalculator.ConvertYToTGrid(Msec, Editor);
        }

        public void UpdateToMsec()
        {
            Msec = TGridCalculator.ConvertTGridToY(TGrid, Editor);
        }
    }
}
