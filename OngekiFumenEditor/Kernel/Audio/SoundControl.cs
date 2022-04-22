using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    [Flags]
    public enum SoundControl
    {
        Tap = 1,
        CriticalTap = 2,
        Hold = 4,
        CriticalHold = 8,
        WallTap = 16,
        CriticalWallTap = 32,
        WallHold = 64,
        CriticalWallHold = 128,
        Flick = 256,
        CriticalFlick = 512,
        Bullet = 1024,
        Beam = 2048,
        Bell = 4096,
        ClickSE = 8192,
        HoldTick = 16384,
        HoldEnd = 32768,

        All = HoldEnd | HoldTick | ClickSE | Bell | Beam | Bullet | CriticalFlick | Flick | CriticalWallHold | WallHold | CriticalWallTap | WallTap | CriticalHold | Hold | CriticalTap | Tap
    }
}
