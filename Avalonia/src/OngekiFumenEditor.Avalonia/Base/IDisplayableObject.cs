namespace OngekiFumenEditor.Avalonia.Base
{
    public interface IDisplayableObject
    {
        public static IEnumerable<IDisplayableObject> Empty { get; } = Array.Empty<IDisplayableObject>();

        bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid);

        IEnumerable<IDisplayableObject> GetDisplayableObjects();
    }
}
