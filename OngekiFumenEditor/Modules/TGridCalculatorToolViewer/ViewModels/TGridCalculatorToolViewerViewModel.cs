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

        private string msecStr;
        public string MsecStr
        {
            get => msecStr;
            set => Set(ref msecStr, value);
        }

        public bool IsEnabled => Editor is not null;

        public override PaneLocation PreferredLocation => PaneLocation.Right;

        public TGridCalculatorToolViewerViewModel()
        {
            DisplayName = "时间计算器";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
        }

        public void UpdateToTGrid()
        {
            TGrid = TGridCalculator.ConvertAudioTimeToTGrid(ParseMsecStr(), Editor);
        }

        private TimeSpan ParseMsecStr()
        {
            if (MsecStr.Contains(":"))
            {
                var revArr = MsecStr.Split(":").Select(x => float.Parse(x)).Reverse().ToArray();
                if (MsecStr.Contains("."))
                {
                    //hh:mm:ss.msec
                    //01:05:500.571
                    return (TimeSpan.FromSeconds(revArr.ElementAtOrDefault(0)) +
                        TimeSpan.FromMinutes(revArr.ElementAtOrDefault(1)) +
                        TimeSpan.FromHours(revArr.ElementAtOrDefault(2))
                        );
                }
                else
                {
                    //mm:ss:msec
                    //01:05:571
                    return (TimeSpan.FromMilliseconds(revArr.ElementAtOrDefault(0)) +
                        TimeSpan.FromSeconds(revArr.ElementAtOrDefault(1)) +
                        TimeSpan.FromMinutes(revArr.ElementAtOrDefault(2))
                        );
                }
            }
            else
            {
                return TimeSpan.FromMilliseconds(float.Parse(MsecStr));
            }
        }

        public void UpdateToMsec()
        {
            MsecStr = TGridCalculator.ConvertTGridToY(TGrid, Editor).ToString();
        }
    }
}
