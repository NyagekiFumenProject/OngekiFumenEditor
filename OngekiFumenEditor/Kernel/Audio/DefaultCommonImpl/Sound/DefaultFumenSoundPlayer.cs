using Caliburn.Micro;
using IntervalTree;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Audio.DefaultCommonImpl.Sound
{
	[Export(typeof(IFumenSoundPlayer))]
	public partial class DefaultFumenSoundPlayer : PropertyChangedBase, IFumenSoundPlayer, IDisposable
	{
		private record MeterAction(TimeSpan Time, TimeSpan BeatInterval, int BeatCount, bool isSkip);

		private IntervalTree<TimeSpan, DurationSoundEvent> durationEvents = new();
		private HashSet<DurationSoundEvent> currentPlayingDurationEvents = new();
		private object locker = new object();

		private LinkedList<SoundEvent> events = new();
		private LinkedListNode<SoundEvent> itor;

		private LinkedList<MeterAction> meterActions = new();
		private LinkedListNode<MeterAction> meterActionsItor;
		private int currentMeterHitCount = 0;

		private AbortableThread thread;

		private IAudioPlayer player;
		private FumenVisualEditorViewModel editor;
		private bool isPlaying = false;
		public bool IsPlaying => isPlaying && (player?.IsPlaying ?? false);
		private static int loopIdGen = 0;

		public SoundControl SoundControl { get; set; } = SoundControl.All;

		private float volume = 1;
		public float Volume
		{
			get => volume;
			set
			{
				Set(ref volume, value);
			}
		}

		private Dictionary<SoundControl, ISoundPlayer> cacheSounds = new();
		private Task<bool> loadTask;

		public DefaultFumenSoundPlayer()
		{
			InitSounds();
		}

		private async void InitSounds()
		{
			var source = new TaskCompletionSource<bool>();
			loadTask = source.Task;
			var audioManager = IoC.Get<IAudioManager>();

			var soundFolderPath = AudioSetting.Default.SoundFolderPath;
			if (!Directory.Exists(soundFolderPath))
			{
				var msg = Resources.ErrorSoundFolderNotFound;
				MessageBox.Show(msg);
				Log.LogError(msg);
				source.SetResult(false);
				return;
			}
			else
				Log.LogInfo($"SoundFolderPath : {soundFolderPath} , fullpath : {Path.GetFullPath(soundFolderPath)}");

			bool noError = true;

			async Task load(SoundControl sound, string fileName)
			{
				var fixFilePath = Path.Combine(soundFolderPath, fileName);

				try
				{
					cacheSounds[sound] = await audioManager.LoadSoundAsync(fixFilePath);
				}
				catch (Exception e)
				{
					Log.LogError($"Can't load {sound} sound file : {fixFilePath} , reason : {e.Message}");
					noError = false;
				}
			}

			cacheSounds.Clear();
			await load(SoundControl.Tap, "tap.wav");
			await load(SoundControl.Bell, "bell.wav");
			await load(SoundControl.CriticalTap, "extap.wav");
			await load(SoundControl.WallTap, "wall.wav");
			await load(SoundControl.CriticalWallTap, "exwall.wav");
			await load(SoundControl.Flick, "flick.wav");
			await load(SoundControl.Bullet, "bullet.wav");
			await load(SoundControl.CriticalFlick, "exflick.wav");
			await load(SoundControl.HoldEnd, "holdend.wav");
			await load(SoundControl.ClickSE, "clickse.wav");
			await load(SoundControl.HoldTick, "holdtick.wav");
			await load(SoundControl.BeamPrepare, "beamprepare.wav");
			await load(SoundControl.BeamLoop, "beamlooping.wav");
			await load(SoundControl.BeamEnd, "beamend.wav");
			await load(SoundControl.MetronomeStrongBeat, "metronomeStrongBeat.wav");
			await load(SoundControl.MetronomeWeakBeat, "metronomeWeakBeat.wav");

			if (!noError)
			{
				MessageBox.Show(Resources.WarnSomeSoundsNotLoad);
				source.SetResult(false);
				return;
			}

			source.SetResult(true);
		}

		public async Task Prepare(FumenVisualEditorViewModel editor, IAudioPlayer player)
		{
			await loadTask;

			if (thread is not null)
			{
				thread.Abort();
				thread = null;
			}

			this.player = player;
			this.editor = editor;

			RebuildEvents();

			thread = new AbortableThread(OnUpdate);
			thread.Name = $"DefaultFumenSoundPlayer_Thread";
			UpdateInternal(thread.CancellationToken);
			thread.Start();
		}

		private static IEnumerable<TGrid> CalculateHoldTicks(Hold x, OngekiFumen fumen)
		{
			int? CalcHoldTickStepSizeA()
			{
				//calculate stepGrid
				var met = fumen.MeterChanges.GetMeter(x.TGrid);
				var bpm = fumen.BpmList.GetBpm(x.TGrid);
				var resT = bpm.TGrid.ResT;
				var beatCount = met.Bunbo;
				if (beatCount == 0)
					return null;
				return (int)(resT / beatCount);
			}

			if (CalcHoldTickStepSizeA() is not int lengthPerBeat)
				yield break;
			var stepGrid = new GridOffset(0, lengthPerBeat);

			var curTGrid = x.TGrid + stepGrid;
			if (x.HoldEnd is null)
				yield break;
			while (curTGrid < x.HoldEnd.TGrid)
			{
				yield return curTGrid;
				curTGrid = curTGrid + stepGrid;
			}
		}

		private static IEnumerable<TGrid> CalculateDefaultClickSEs(OngekiFumen fumen)
		{
			var tGrid = TGrid.Zero;
			var endTGrid = new TGrid(1, 0);
			//calculate stepGrid
			var met = fumen.MeterChanges.GetMeter(tGrid);
			var bpm = fumen.BpmList.GetBpm(tGrid);
			var resT = bpm.TGrid.ResT;
			var beatCount = met.Bunbo * 1;
			if (beatCount != 0)
			{
				var lengthPerBeat = (int)(resT / beatCount);

				var stepGrid = new GridOffset(0, lengthPerBeat);

				var curTGrid = tGrid + stepGrid;
				while (curTGrid < endTGrid)
				{
					yield return curTGrid;
					curTGrid = curTGrid + stepGrid;
				}
			}
		}

		private void RebuildEvents()
		{
			StopAllLoop();
			events.ForEach(ObjectPool<SoundEvent>.Return);
			durationEvents.Select(x => x.Value).ForEach(ObjectPool<DurationSoundEvent>.Return);
			events.Clear();
			durationEvents.Clear();
			currentPlayingDurationEvents.Clear();

			var list = new HashSet<SoundEvent>();
			var durationList = new HashSet<DurationSoundEvent>();

			void AddSound(SoundControl sound, TGrid tGrid)
			{
				var evt = ObjectPool<SoundEvent>.Get();

				evt.Sounds = sound;
				evt.Time = TGridCalculator.ConvertTGridToAudioTime(tGrid, editor);
				//evt.TGrid = tGrid;

				list.Add(evt);
			}

			void AddDurationSound(SoundControl sound, TGrid tGrid, TGrid endTGrid, int loopId = 0)
			{
				var evt = ObjectPool<DurationSoundEvent>.Get();

				evt.Sounds = sound;
				evt.LoopId = loopId;
				evt.Time = TGridCalculator.ConvertTGridToAudioTime(tGrid, editor);
				evt.EndTime = TGridCalculator.ConvertTGridToAudioTime(endTGrid, editor);
				//evt.TGrid = tGrid;

				durationList.Add(evt);
			}

			var fumen = editor.Fumen;

			var soundObjects = fumen.GetAllDisplayableObjects().OfType<OngekiTimelineObjectBase>();

			//add default clickse objects.
			if (!fumen.ClickSEs.Any(x => x.TGrid.TotalUnit <= 1))
			{
				foreach (var tGrid in CalculateDefaultClickSEs(fumen))
					AddSound(SoundControl.ClickSE, tGrid);
			}

			using var _d = ObjectPool<HashSet<Type>>.GetWithUsingDisposable(out var typeSet, out _);

			foreach (var group in soundObjects.GroupBy(x => x.TGrid))
			{
				var sounds = (SoundControl)0;
				typeSet.Clear();

				foreach (var obj in group.Where(x =>
				{
					if (x is Tap)
						return true;
					return typeSet.Add(x.GetType());
				}))
				{
					sounds = sounds | obj switch
					{
						Tap { ReferenceLaneStart: { IsWallLane: true }, IsCritical: false } or Hold { ReferenceLaneStart: { IsWallLane: true }, IsCritical: false } => SoundControl.WallTap,
						Tap { ReferenceLaneStart: { IsWallLane: true }, IsCritical: true } or Hold { ReferenceLaneStart: { IsWallLane: true }, IsCritical: true } => SoundControl.CriticalWallTap,
						Tap { ReferenceLaneStart: { IsWallLane: false }, IsCritical: false } or Hold { ReferenceLaneStart: { IsWallLane: false }, IsCritical: false } => SoundControl.Tap,
						Tap { ReferenceLaneStart: { IsWallLane: false }, IsCritical: true } or Hold { ReferenceLaneStart: { IsWallLane: false }, IsCritical: true } => SoundControl.CriticalTap,
						Tap { ReferenceLaneStart: null, IsCritical: false } or Hold { ReferenceLaneStart: null, IsCritical: false } => SoundControl.Tap,
						Tap { ReferenceLaneStart: null, IsCritical: true } or Hold { ReferenceLaneStart: null, IsCritical: true } => SoundControl.CriticalTap,
						Bell => SoundControl.Bell,
						Bullet => SoundControl.Bullet,
						Flick { IsCritical: false } => SoundControl.Flick,
						Flick { IsCritical: true } => SoundControl.CriticalFlick,
						HoldEnd => SoundControl.HoldEnd,
						ClickSE => SoundControl.ClickSE,
						_ => default
					};

					if (obj is Hold hold)
					{
						//add hold ticks
						foreach (var tickTGrid in CalculateHoldTicks(hold, fumen))
						{
							AddSound(SoundControl.HoldTick, tickTGrid);
						}
					}

					if (obj is BeamStart beam)
					{
						var loopId = ++loopIdGen;

						//generate stop
						AddSound(SoundControl.BeamEnd, beam.MaxTGrid);
						AddDurationSound(SoundControl.BeamLoop, beam.TGrid, beam.MaxTGrid, loopId);
						var leadBodyInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(TGridCalculator.ConvertTGridToAudioTime(beam.TGrid, editor) - TimeSpan.FromMilliseconds(BeamStart.LEAD_IN_DURATION), editor);
						if (leadBodyInTGrid is null)
							leadBodyInTGrid = TGrid.Zero;
						AddSound(SoundControl.BeamPrepare, leadBodyInTGrid);
					}
				}
				if (sounds != 0)
					AddSound(sounds, group.Key);
			}
			events = new LinkedList<SoundEvent>(list.OrderBy(x => x.Time));
			foreach (var durationEvent in durationList)
				durationEvents.Add(durationEvent.Time, durationEvent.EndTime, durationEvent);
			itor = null;

			meterActions.Clear();
			if (EditorGlobalSetting.Default.LoopPlayTiming)
			{
				var oneTGrid = new TGrid(1, 0);

				var timeSignatureList = fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(fumen.BpmList);
				foreach (var timeSignature in timeSignatureList)
				{
					var beatCount = timeSignature.meter.Bunbo;
					var isSkip = beatCount == 0;
					var beatInterval = isSkip ? default :
						TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(TGrid.Zero, oneTGrid, timeSignature.bpm.BPM))
						/ beatCount;

					var action = new MeterAction(timeSignature.audioTime, beatInterval, beatCount, isSkip);
					meterActions.AddLast(action);
				}
			}
			meterActionsItor = default;
			currentMeterHitCount = 0;
		}

		private void UpdateInternal(CancellationToken token)
		{
			if ((itor is null && meterActionsItor is null) || player is null || token.IsCancellationRequested)
				return;
			if (!IsPlaying)
			{
				//stop all looping
				StopAllLoop();
				return;
			}

			var currentTime = player.CurrentTime;

			//播放物件音效
			while (itor is not null)
			{
				var nextBeatTime = itor.Value.Time.TotalMilliseconds;
				var ct = currentTime.TotalMilliseconds - nextBeatTime;
				if (ct >= 0)
				{
					//Debug.WriteLine($"diff:{ct:F2}ms, target:{itor.Value}, currentTime:{currentTime}");
					PlaySoundsOnce(itor.Value.Sounds);
					itor = itor.Next;
				}
				else
					break;
			}

			//播放节拍器
			while (meterActionsItor is not null)
			{
				var nextActionItor = meterActionsItor.Next;

				//检查当前是否有效
				if (meterActionsItor.Value.isSkip)
				{
					meterActionsItor = nextActionItor;
					currentMeterHitCount = 0;
					continue;
				}

				var nextBeatTime = meterActionsItor.Value.Time +
					meterActionsItor.Value.BeatInterval * currentMeterHitCount;

				//检查是否超过下一个
				if (nextActionItor != null)
				{
					if (nextBeatTime > nextActionItor.Value.Time)
					{
						meterActionsItor = nextActionItor;
						currentMeterHitCount = 0;
						continue;
					}
				}

				//没超过就检查了
				var ct = currentTime.TotalMilliseconds - nextBeatTime.TotalMilliseconds;
				if (ct >= 0)
				{
					//Log.LogDebug($"currentMeterHitCount:{currentMeterHitCount}, nextBeatTime:{nextBeatTime}, diff:{ct:F2}ms, meterActionsItor:{meterActionsItor.Value}");
					var beatIdx = currentMeterHitCount % meterActionsItor.Value.BeatCount;
					var sound = beatIdx == 0 ? SoundControl.MetronomeStrongBeat : SoundControl.MetronomeWeakBeat;
					PlaySoundsOnce(sound);
					currentMeterHitCount++;
				}
				else
					break;
			}

			//检查循环音效
			lock (locker)
			{
				var queryDurationEvents = durationEvents.Query(currentTime);
				foreach (var durationEvent in queryDurationEvents)
				{
					//检查是否正在播放了
					if (!currentPlayingDurationEvents.Contains(durationEvent))
					{
						if (SoundControl.HasFlag(durationEvent.Sounds) && cacheSounds.TryGetValue(durationEvent.Sounds, out var soundPlayer))
						{
							var initPlayTime = currentTime - durationEvent.Time;
							soundPlayer.PlayLoop(durationEvent.LoopId, initPlayTime);

							currentPlayingDurationEvents.Add(durationEvent);
						}
					}
				}
				//检查是否已经播放完成
				foreach (var durationEvent in currentPlayingDurationEvents.Where(x => currentTime < x.Time || currentTime > x.EndTime).ToArray())
				{
					if (cacheSounds.TryGetValue(durationEvent.Sounds, out var soundPlayer))
					{
						soundPlayer.StopLoop(durationEvent.LoopId);
						currentPlayingDurationEvents.Remove(durationEvent);
					}
				}
			}

			/*
            else
            {
                var sleepTime = Math.Min(1000, (int)((Math.Abs(ct) - 2) * player.Speed));
                if (ct < -5 && sleepTime > 0)
                    Thread.Sleep(sleepTime);
                break;
            }*/
		}

		private void OnUpdate(CancellationToken cancel)
		{
			while (!cancel.IsCancellationRequested)
			{
				UpdateInternal(cancel);
			}
		}

		private void PlaySoundsOnce(SoundControl sounds)
		{
			void checkPlay(SoundControl subFlag)
			{
				if (sounds.HasFlag(subFlag) && SoundControl.HasFlag(subFlag) && cacheSounds.TryGetValue(subFlag, out var sound))
					sound.PlayOnce();
			}

			checkPlay(SoundControl.Tap);
			checkPlay(SoundControl.CriticalTap);
			checkPlay(SoundControl.Bell);
			checkPlay(SoundControl.WallTap);
			checkPlay(SoundControl.CriticalWallTap);
			checkPlay(SoundControl.Bullet);
			checkPlay(SoundControl.Flick);
			checkPlay(SoundControl.CriticalFlick);
			checkPlay(SoundControl.HoldEnd);
			checkPlay(SoundControl.HoldTick);
			checkPlay(SoundControl.ClickSE);
			checkPlay(SoundControl.BeamPrepare);
			checkPlay(SoundControl.BeamEnd);
			checkPlay(SoundControl.MetronomeStrongBeat);
			checkPlay(SoundControl.MetronomeWeakBeat);
		}

		public void Seek(TimeSpan msec, bool pause)
		{
			Pause();
			itor = events.Find(events.FirstOrDefault(x => msec < x.Time));
			meterActionsItor = meterActions.Find(meterActions.LastOrDefault(x => msec >= x.Time));
			if (meterActionsItor is null)
				currentMeterHitCount = 0;
			else
			{
				if (meterActionsItor.Value.isSkip)
					currentMeterHitCount = 0;
				else
					currentMeterHitCount = (int)((msec - meterActionsItor.Value.Time) / meterActionsItor.Value.BeatInterval);
			}

			if (!pause)
				PlayInternal();
		}

		private void StopAllLoop()
		{
			lock (locker)
			{
				foreach (var durationEvent in currentPlayingDurationEvents.ToArray())
				{
					if (durationEvent is null)
						continue;
					if (cacheSounds.TryGetValue(durationEvent.Sounds, out var soundPlayer))
					{
						soundPlayer.StopLoop(durationEvent.LoopId);
						currentPlayingDurationEvents.Remove(durationEvent);
					}
				}
			}
		}

		public void Stop()
		{
			thread?.Abort();
			StopAllLoop();
			isPlaying = false;
		}

		public void PlayInternal()
		{
			if (player is null)
				return;
			isPlaying = true;
		}

		public void Play()
		{
			if (player is null)
				return;
			itor = itor ?? events.First;
			meterActionsItor = meterActionsItor ?? meterActions.First;
			currentMeterHitCount = 0;

			PlayInternal();
		}

		public void Pause()
		{
			isPlaying = false;
			StopAllLoop();
		}

		public void Dispose()
		{
			thread?.Abort();
			foreach (var sound in cacheSounds.Values)
				sound.Dispose();
		}

		public Task Clean()
		{
			Stop();

			thread = null;

			player = null;
			editor = null;

			events.Clear();

			return Task.CompletedTask;
		}

		public float? GetVolume(SoundControl sound)
		{
			foreach (var item in cacheSounds)
			{
				if (item.Key == sound)
				{
					return item.Value.Volume;
				}
			}

			return null;
		}

		public void SetVolume(SoundControl sound, float volume)
		{
			foreach (var item in cacheSounds)
			{
				if (item.Key == sound)
				{
					item.Value.Volume = volume;
				}
			}
		}

		public async Task<bool> ReloadSoundFiles()
		{
			InitSounds();
			return await loadTask;
		}
	}
}
