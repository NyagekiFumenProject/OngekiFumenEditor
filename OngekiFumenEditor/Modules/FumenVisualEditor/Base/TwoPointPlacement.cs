namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    /// <summary>两点放置模式；取消或完成后不会保留上次的起点。</summary>
    public sealed class TwoPointPlacement<T>
    {
        public bool IsActive { get; private set; }
        public bool HasFirstPoint { get; private set; }
        private T firstPoint;

        public void Toggle()
        {
            var activate = !IsActive;
            Cancel();
            IsActive = activate;
        }

        public void Cancel()
        {
            IsActive = false;
            HasFirstPoint = false;
            firstPoint = default;
        }

        public bool TryComplete(T point, out T first)
        {
            first = default;
            if (!IsActive)
                return false;
            if (!HasFirstPoint)
            {
                firstPoint = point;
                HasFirstPoint = true;
                return false;
            }
            first = firstPoint;
            Cancel();
            return true;
        }
    }
}
