using Caliburn.Micro;
using ExtrameFunctionCalculator.Script.Types;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public class BeamConnector : PropertyChangedBase, IDisplayableObject
    {
        public Type ModelViewType => typeof(BeamConnectorViewModel);

        private BeamBase from;
        public BeamBase From
        {
            get => from;
            set => Set(ref from, value);
        }

        private BeamBase to;

        public event EventHandler<ViewAttachedEventArgs> ViewAttached;

        public BeamBase To
        {
            get => to;
            set => Set(ref to, value);
        }

        public bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return (From?.CheckVisiable(minVisibleTGrid, maxVisibleTGrid) ?? false) || (To?.CheckVisiable(minVisibleTGrid, maxVisibleTGrid) ?? false);
        }

        public IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
        }
    }
}
