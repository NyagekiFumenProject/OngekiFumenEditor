using ManagedBass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.BassImpl.Base
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class FXVolumeParam : IEffectParameter
    {
        public float fTarget;
        public float fCurrent;
        public float fTime;
        public uint lCurve;

        public EffectType FXType => EffectType.Volume;
    }
}
