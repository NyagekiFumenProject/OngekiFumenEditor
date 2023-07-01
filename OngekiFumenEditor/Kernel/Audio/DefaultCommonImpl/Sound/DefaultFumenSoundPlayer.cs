using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace OngekiFumenEditor.Kernel.Audio.DefaultCommonImpl.Sound
{
    [Export(typeof(IFumenSoundPlayer))]
    public class DefaultFumenSoundPlayer : PropertyChangedBase, IFumenSoundPlayer, IDisposable
    {
        [Flags]
        public enum Sound
        {
            Tap = 1,
            ExTap = 2,

            WallTap = 8,
            WallExTap = 16,

            Bell = 32,
            Bullet = 64,

            Hold = 128,

            Flick = 256,
            ExFlick = 512,

            HoldEnd = 1024,
            ClickSE = 2048,
            HoldTick = 4096,
        }

        public class SoundEvent
        {
            public Sound Sounds { get; set; }
            public TimeSpan Time { get; set; }
            //public TGrid TGrid { get; set; }

            public override string ToString() => $"{Time} {Sounds}";
        }

        private LinkedList<SoundEvent> events = new();
        private LinkedListNode<SoundEvent> itor;
        private AbortableThread thread;

        private IAudioPlayer player;
        private FumenVisualEditorViewModel editor;
        private bool isPlaying = false;
        public bool IsPlaying => isPlaying && (player?.IsPlaying ?? false);

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

        private Dictionary<Sound, ISoundPlayer> cacheSounds = new();
        private Task loadTask;

        public DefaultFumenSoundPlayer()
        {
            InitSounds();
        }

        private async void InitSounds()
        {
            var source = new TaskCompletionSource();
            loadTask = source.Task;
            var audioManager = IoC.Get<IAudioManager>();

            var soundFolderPath = AudioSetting.Default.SoundFolderPath;
            if (!Directory.Exists(soundFolderPath))
            {
                var msg = $"因为音效文件夹不存在,无法加载音效";
                MessageBox.Show(msg);
                Log.LogError(msg);
            }
            else
                Log.LogInfo($"SoundFolderPath : {soundFolderPath} , fullpath : {Path.GetFullPath(soundFolderPath)}");

            bool noError = true;

            async Task load(Sound sound, string fileName)
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

            await load(Sound.Tap, "tap.wav");
            await load(Sound.Bell, "bell.wav");
            await load(Sound.ExTap, "extap.wav");
            await load(Sound.WallTap, "wall.wav");
            await load(Sound.WallExTap, "exwall.wav");
            await load(Sound.Flick, "flick.wav");
            await load(Sound.Bullet, "bullet.wav");
            await load(Sound.ExFlick, "exflick.wav");
            await load(Sound.HoldEnd, "holdend.wav");
            await load(Sound.ClickSE, "clickse.wav");
            await load(Sound.HoldTick, "holdtick.wav");

            if (!noError)
                MessageBox.Show("部分音效并未加载成功,详情可查看日志");

            source.SetResult();
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
                var beatCount = met.BunShi * 1;
                if (beatCount == 0)
                    return null;
                return (int)(resT / beatCount);
            }
            /*
            int? CalcHoldTickStepSizeB()
            {
                var bpm = fumen.BpmList.GetBpm(x.TGrid).BPM;
                var progressJudgeBPM = fumen.MetaInfo.ProgJudgeBpm;
                var standardBeatLen = fumen.MetaInfo.TRESOLUTION >> 2; //取1/4切片长度

                if (bpm < progressJudgeBPM)
                {
                    while (bpm < progressJudgeBPM)
                    {
                        standardBeatLen >>= 1;
                        bpm *= 2f;
                    }
                }
                else
                {
                    for (progressJudgeBPM *= 2f; progressJudgeBPM <= bpm; progressJudgeBPM *= 2f)
                    {
                        standardBeatLen <<= 1;
                    }
                }
                return standardBeatLen;
            }
            */

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
            var beatCount = met.BunShi * 1;
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
            events.ForEach(evt => ObjectPool<SoundEvent>.Return(evt));
            events.Clear();

            var list = new HashSet<SoundEvent>();

            void AddSound(Sound sound, TGrid tGrid)
            {
                var evt = ObjectPool<SoundEvent>.Get();

                evt.Sounds = sound;
                evt.Time = TGridCalculator.ConvertTGridToAudioTime(tGrid, editor);
                //evt.TGrid = tGrid;

                list.Add(evt);
            }

            var fumen = editor.Fumen;

            var soundObjects = fumen.GetAllDisplayableObjects().OfType<OngekiTimelineObjectBase>();

            //add default clickse objects.
            foreach (var tGrid in CalculateDefaultClickSEs(fumen))
                AddSound(Sound.ClickSE, tGrid);

            foreach (var group in soundObjects.GroupBy(x => x.TGrid))
            {
                var sounds = (Sound)0;

                foreach (var obj in group.DistinctBy(x => x.GetType()))
                {
                    sounds = sounds | obj switch
                    {
                        WallTap { IsCritical: false } => Sound.WallTap,
                        WallTap { IsCritical: true } => Sound.WallExTap,
                        Tap { IsCritical: false } or Hold { IsCritical: false } => Sound.Tap,
                        Tap { IsCritical: true } or Hold { IsCritical: true } => Sound.ExTap,
                        Bell => Sound.Bell,
                        Bullet => Sound.Bullet,
                        Flick { IsCritical: false } => Sound.Flick,
                        Flick { IsCritical: true } => Sound.ExFlick,
                        HoldEnd => Sound.HoldEnd,
                        ClickSE => Sound.ClickSE,
                        _ => default
                    };

                    if (obj is Hold hold)
                    {
                        //add hold ticks
                        foreach (var tickTGrid in CalculateHoldTicks(hold, fumen))
                        {
                            AddSound(Sound.HoldTick, tickTGrid);
                        }
                    }
                }

                if (sounds != 0)
                    AddSound(sounds, group.Key);
            }

            events = new LinkedList<SoundEvent>(list.OrderBy(x => x.Time));

            itor = events.First;
        }

        private void OnUpdate(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                if (itor is null || player is null || !IsPlaying)
                    continue;

                while (itor is not null)
                {
                    var currentTime = player.CurrentTime.TotalMilliseconds;
                    var itorTime = itor.Value.Time.TotalMilliseconds;
                    var ct = currentTime - itorTime;
                    if (ct >= 0)
                    {
                        //Debug.WriteLine($"diff:{ct:F2}ms target:{itor.Value}");
                        PlaySounds(itor.Value.Sounds);
                        itor = itor.Next;
                    }
                    else
                    {
                        if (ct < -5)
                            Thread.Sleep(Math.Min(1000, (int)(Math.Abs(ct) - 2)));
                        break;
                    }
                }
            }
        }

        private void PlaySounds(Sound sounds)
        {
            void checkPlay(Sound subFlag, SoundControl control)
            {
                if (sounds.HasFlag(subFlag) && SoundControl.HasFlag(control) && cacheSounds.TryGetValue(subFlag, out var sound))
                    sound.PlayOnce();
            }

            checkPlay(Sound.Tap, SoundControl.Tap);
            checkPlay(Sound.ExTap, SoundControl.CriticalTap);
            checkPlay(Sound.Bell, SoundControl.Bell);
            checkPlay(Sound.WallTap, SoundControl.WallTap);
            checkPlay(Sound.WallExTap, SoundControl.CriticalWallTap);
            checkPlay(Sound.Bullet, SoundControl.Bullet);
            checkPlay(Sound.Flick, SoundControl.Flick);
            checkPlay(Sound.ExFlick, SoundControl.CriticalFlick);
            checkPlay(Sound.HoldEnd, SoundControl.HoldEnd);
            checkPlay(Sound.HoldTick, SoundControl.HoldTick);
            checkPlay(Sound.ClickSE, SoundControl.ClickSE);
        }

        public void Seek(TimeSpan msec, bool pause)
        {
            Pause();
            itor = events.Find(events.FirstOrDefault(x => msec < x.Time));

            if (!pause)
                Play();
        }

        public void Stop()
        {
            thread?.Abort();
            isPlaying = false;
        }

        public void Play()
        {
            if (player is null)
                return;
            isPlaying = true;
        }

        public void Pause()
        {
            isPlaying = false;
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

        public float GetVolume(Sound sound)
        {
            foreach (var item in cacheSounds)
            {
                if (item.Key == sound)
                {
                    return item.Value.Volume;
                }
            }

            return 0;
        }

        public void SetVolume(Sound sound, float volume)
        {
            foreach (var item in cacheSounds)
            {
                if (item.Key == sound)
                {
                    item.Value.Volume = volume;
                }
            }
        }
    }
}
