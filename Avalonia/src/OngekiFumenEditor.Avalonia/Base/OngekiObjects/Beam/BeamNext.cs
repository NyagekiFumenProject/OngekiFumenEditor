using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam
{
	public class BeamNext : ConnectableChildObjectBase, IBeamObject
	{
		public override string IDShortName => (IsObliqueBeam ? "OB" : "BM") + (IsEndObject ? "E" : "N");

		public bool IsObliqueBeam => ObliqueSourceXGridOffset is not null;

		private WidthId widthId = WidthIdConst.Id_1;
        public WidthId WidthId
		{
			get => widthId;
			set => SetProperty(ref widthId, value);
		}

		private XGrid obliqueSourceXGrid = null;
		[ObjectPropertyBrowserAllowSetNull]
		public XGrid ObliqueSourceXGridOffset
		{
			get { return obliqueSourceXGrid ?? (ReferenceStartObject as IBeamObject)?.ObliqueSourceXGridOffset; }
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(obliqueSourceXGrid, value);
				obliqueSourceXGrid = value;
				OnPropertyChanged();
			}
		}

        public override void Dispose()
        {
            base.Dispose();
            ObliqueSourceXGridOffset = default;
        }
    }
}

