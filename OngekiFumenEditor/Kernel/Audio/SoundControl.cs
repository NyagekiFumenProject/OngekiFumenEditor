using System;

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
		BeamPrepare = HoldEnd * 2,
		BeamLoop = BeamPrepare * 2,
		BeamEnd = BeamLoop * 2,
		MetronomeStrongBeat = BeamEnd * 2,
		MetronomeWeakBeat = MetronomeStrongBeat * 2,

		All = MetronomeStrongBeat | MetronomeWeakBeat | BeamPrepare | BeamLoop | BeamEnd | HoldEnd | HoldTick | ClickSE | Bell | Beam | Bullet | CriticalFlick | Flick | CriticalWallHold | WallHold | CriticalWallTap | WallTap | CriticalHold | Hold | CriticalTap | Tap
	}
}
