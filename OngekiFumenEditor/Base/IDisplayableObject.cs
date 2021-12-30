using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base
{
    public interface IDisplayableObject
    {
        Type ModelViewType { get; }

        bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid);

        IEnumerable<IDisplayableObject> GetDisplayableObjects();
    }
}