using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Base
{
    public interface IDisplayableObject
    {
        bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid);

        IEnumerable<IDisplayableObject> GetDisplayableObjects();
    }
}
