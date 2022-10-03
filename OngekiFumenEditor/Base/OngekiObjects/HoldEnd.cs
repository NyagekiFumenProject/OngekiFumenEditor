using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class HoldEnd : ConnectableEndObject, ILaneDockable
    {

        public bool IsCritical => (ReferenceStartObject as Hold)?.IsCritical ?? false;

        public override string IDShortName => "[HoldEnd]";

        public LaneStartBase ReferenceLaneStart
        {
            get => (ReferenceStartObject as Hold)?.ReferenceLaneStart;
            set
            {
                NotifyOfPropertyChange(() => ReferenceLaneStart);
            }
        }

        public int ReferenceLaneStrId
        {
            get => (ReferenceStartObject as Hold)?.ReferenceLaneStrId ?? -1;
            set => NotifyOfPropertyChange(() => ReferenceLaneStrId);
        }
    }
}
