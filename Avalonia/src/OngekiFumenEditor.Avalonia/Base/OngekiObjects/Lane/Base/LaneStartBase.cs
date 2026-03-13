using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base
{
    public abstract class LaneStartBase : ConnectableStartObject
    {
        private bool isTransparent;
        public bool IsTransparent
        {
            get => isTransparent;
            set => SetProperty(ref isTransparent, value);
        }
    }
}
