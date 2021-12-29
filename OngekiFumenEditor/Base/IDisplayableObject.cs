using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base
{
    public interface IDisplayableObject
    {
        public Type ModelViewType { get; }

        public virtual IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
        }
    }
}