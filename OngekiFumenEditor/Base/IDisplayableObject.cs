using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Base
{
    public interface IDisplayableObject
    {
        static IEnumerable<IDisplayableObject> EmptyDisplayable { get; } = new IDisplayableObject[0];

        Type ModelViewType { get; }

        bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid);

        IEnumerable<IDisplayableObject> GetDisplayableObjects();
    }
}