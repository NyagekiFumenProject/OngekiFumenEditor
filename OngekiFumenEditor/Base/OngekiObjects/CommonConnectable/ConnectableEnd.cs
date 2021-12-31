using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.CommonConnectable
{
    public abstract class ConnectableEnd<T> : ConnectableChildBase where T : ConnectableEnd<T>, new()
    {
        public override Type ModelViewType => typeof(ConnectableEndViewModel<T>);
    }
}
