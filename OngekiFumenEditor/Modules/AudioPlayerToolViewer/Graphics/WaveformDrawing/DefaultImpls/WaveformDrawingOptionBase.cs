using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
	public abstract class WaveformDrawingOptionBase : PropertyChangedBase, IWaveformDrawingOption
	{
		[ObjectPropertyBrowserHide]
		public override bool IsNotifying
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => base.IsNotifying;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => base.IsNotifying = value;
		}

        public abstract void Reload();
        public abstract void Reset();
        public abstract void Save();
    }
}
