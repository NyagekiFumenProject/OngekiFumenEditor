using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public interface IBulletPalleteReferencable : IDisplayableObject, IHorizonPositionObject, ITimelineObject, INotifyPropertyChanged
    {
        BulletPallete ReferenceBulletPallete { get; set; }
    }
}
