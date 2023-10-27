using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
	public class WaveformDrawingOptionBase : PropertyChangedBase, IWaveformDrawingOption
	{
		[ObjectPropertyBrowserHide]
		public override bool IsNotifying
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => base.IsNotifying;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => base.IsNotifying = value;
		}
	}
}
