using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
