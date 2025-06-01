using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane.Base
{
    public abstract class LaneStartBase : ConnectableStartObject
    {
        private bool isTransparent;
        public bool IsTransparent
        {
            get => isTransparent;
            set => Set(ref isTransparent, value);
        }
    }
}
