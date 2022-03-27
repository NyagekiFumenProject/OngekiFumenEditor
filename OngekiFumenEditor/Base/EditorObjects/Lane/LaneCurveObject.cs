using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects.Lane
{
    public class LaneCurveObject : ConnectableChildObjectBase
    {
        private bool isSelecting;
        public bool IsSelecting
        {
            get => isSelecting;
            set => Set(ref isSelecting, value);
        }

        private List<LaneCurvePathControlObject> pathControls = new();
        public IReadOnlyList<LaneCurvePathControlObject> PathControls => pathControls;

        public void AddControlObject(LaneCurvePathControlObject controlObj)
        {
#if DEBUG
            if (controlObj.RefCurveObject is not null)
                throw new Exception("controlObj is using");
#endif

            pathControls.Add(controlObj);
            controlObj.PropertyChanged += ControlObj_PropertyChanged;
            controlObj.RefCurveObject = this;
            NotifyOfPropertyChange(() => PathControls);
        }

        public void RemoveControlObject(LaneCurvePathControlObject controlObj)
        {
            if (pathControls.Remove(controlObj))
            {
                controlObj.RefCurveObject = null;
                controlObj.PropertyChanged -= ControlObj_PropertyChanged;
                NotifyOfPropertyChange(() => PathControls);
            }
        }

        private void ControlObj_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TGrid):
                case nameof(XGrid):
                    NotifyOfPropertyChange(e.PropertyName);
                    break;
                default:
                    break;
            }
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            return PathControls.AsEnumerable<IDisplayableObject>().Append(this);
        }

        public LaneType LaneType => (ReferenceStartObject as LaneStartBase)?.LaneType ?? LaneType.Center;

        public override Type ModelViewType => typeof(LaneCurveObjectViewModel);

        public override string IDShortName => "LCO";
    }
}
