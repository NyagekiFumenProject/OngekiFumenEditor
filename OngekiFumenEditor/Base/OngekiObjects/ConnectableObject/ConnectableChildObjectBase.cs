using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableChildObjectBase : ConnectableObjectBase
    {
        private bool isSelecting;
        public bool IsSelecting
        {
            get => isSelecting;
            set => Set(ref isSelecting, value);
        }

        public ConnectableStartObject ReferenceStartObject { get; set; }
        public ConnectableObjectBase PrevObject { get; set; }
        public override int RecordId { get => ReferenceStartObject?.RecordId ?? int.MinValue; set { } }
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

        public void RemoveControlObject(LaneCurvePathControlObject controlObj)
        {
            if (pathControls.Remove(controlObj))
            {
                controlObj.RefCurveObject = null;
                controlObj.PropertyChanged -= ControlObj_PropertyChanged;
                NotifyOfPropertyChange(() => PathControls);
            }
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            return PathControls.AsEnumerable<IDisplayableObject>().Append(this);
        }

        public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return base.CheckVisiable(minVisibleTGrid, maxVisibleTGrid) || (TGrid > maxVisibleTGrid && PrevObject is not null && PrevObject.TGrid < minVisibleTGrid);
        }

        public override string ToString() => $"{base.ToString()} {RecordId} Ref:{ReferenceStartObject} {(PathControls.Count > 0 ? $"CurveCount:{PathControls.Count}" : string.Empty)}";
    }
}
