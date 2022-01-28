using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class HoldEnd : ConnectableEndObject, ILaneDockable
    {
        public override Type ModelViewType => typeof(HoldEndViewModel);

        public bool IsCritical => ((Hold)ReferenceStartObject).IsCritical;

        public override string IDShortName => "[HoldEnd]";

        public LaneStartBase ReferenceLaneStart {
            get => (ReferenceStartObject as Hold)?.ReferenceLaneStart;
            set
            {
                //ignore it :D
            }
        }
    }
}
