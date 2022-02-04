using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.ViewModels
{
    [Export(typeof(IFumenTimeSignatureListViewer))]
    public class FumenTimeSignatureListViewerViewModel : Tool, IFumenTimeSignatureListViewer
    {
        public struct DisplayTimeSignatureItem
        {
            public float StartY { get; set; }
            public TGrid StartTGrid { get; set; }
            public MeterChange Meter { get; set; }
            public BPMChange BPMChange { get; set; }
        }

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        public ObservableCollection<DisplayTimeSignatureItem> DisplayTimeSignatures { get; } = new();

        private FumenVisualEditorViewModel editor;
        public FumenVisualEditorViewModel Editor
        {
            get => editor;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(editor, value, OnEditorPropertyChanged);
                Set(ref editor, value);
                RefreshFumen();
            }
        }

        private DisplayTimeSignatureItem currentSelectTimeSignature;
        public DisplayTimeSignatureItem CurrentSelectTimeSignature
        {
            get => currentSelectTimeSignature;
            set
            {
                Set(ref currentSelectTimeSignature, value);
            }
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
                RefreshFumen();
        }

        public FumenTimeSignatureListViewerViewModel()
        {
            DisplayName = "节拍查看器";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, o) => Editor = n;
        }

        private void RefreshFumen()
        {
            DisplayTimeSignatures.Clear();
            var list = Editor.Fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(240, Editor.Fumen.BpmList);
            foreach (var ts in list)
                DisplayTimeSignatures.Add(new DisplayTimeSignatureItem()
                {
                    BPMChange = ts.bpm,
                    Meter = ts.meter,
                    StartTGrid = ts.startTGrid,
                    StartY = (float)ts.startY
                });
        }
    }
}
