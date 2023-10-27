using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound
{
	internal class NAudioSoundPlayer : ISoundPlayer
	{
		public TimeSpan Duration => cacheSound.Duration;

		private NAudioManager soundManager = default;
		private CachedSound cacheSound = default;
		private Dictionary<int, ILoopHandle> loopMap = new Dictionary<int, ILoopHandle>();

		public NAudioSoundPlayer(CachedSound cache, NAudioManager manager)
		{
			soundManager = manager;
			cacheSound = cache;
		}

		public float Volume { get; set; } = 1;

		public void Dispose()
		{

		}

		public void PlayOnce()
		{
			soundManager.PlaySound(cacheSound, Volume, TimeSpan.Zero);
		}

		public void PlayLoop(int loopId, TimeSpan init)
		{
			if (!loopMap.ContainsKey(loopId))
			{
				var handle = soundManager.PlayLoopSound(cacheSound, Volume, init);
				loopMap[loopId] = handle;
			}
			else
			{
				//todo warn
			}
		}

		public void StopLoop(int loopId)
		{
			if (loopMap.TryGetValue(loopId, out var handle))
			{
				soundManager.StopLoopSound(handle);
				loopMap.Remove(loopId);
			}
			else
			{
				//todo warn
			}
		}
	}
}
