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

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    [Export(typeof(IFumenSoundPlayer))]
    public class DefaultFumenSoundPlayer : PropertyChangedBase, IFumenSoundPlayer, IDisposable
    {
        [Flags]
        public enum Sound
        {
            Tap = 1,
            ExTap = 2,
            Wall = 4,
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
            public double Time { get; set; }

            public override string ToString() => $"{Time:F2} {Sounds}";
        }

        private LinkedList<SoundEvent> events = new();
        private LinkedListNode<SoundEvent> itor;
        private AbortableThread thread;

        private IAudioPlayer player;
        private FumenVisualEditorViewModel editor;
        private bool isPlaying = false;
        public bool IsPlaying => isPlaying && player.IsPlaying;

        public SoundControl SoundControl { get; set; } = SoundControl.All;

        public double CurrentTime => player.CurrentTime + editor.Setting.SoundOffset;

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
            Log.LogError($"SoundFolderPath : {soundFolderPath} , fullpath : {Path.GetFullPath(soundFolderPath)}");

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
                }
            }

            await load(Sound.Tap, "tap.wav");
            await load(Sound.Bell, "bell.wav");
            await load(Sound.ExTap, "extap.wav");
            await load(Sound.Wall, "wall.wav");
            await load(Sound.WallExTap, "exwall.wav");
            await load(Sound.Flick, "flick.wav");
            await load(Sound.Bullet, "bullet.wav");
            await load(Sound.ExFlick, "exflick.wav");
            await load(Sound.HoldEnd, "holdend.wav");
            await load(Sound.ClickSE, "clickse.wav");
            await load(Sound.HoldTick, "holdtick.wav");

            source.SetResult();
        }

        public async Task Init(FumenVisualEditorViewModel editor, IAudioPlayer player)
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
            thread.Start();
        }

        private void RebuildEvents()
        {
            events.ForEach(evt => ObjectPool<SoundEvent>.Return(evt));
            events.Clear();

            var fumen = editor.Fumen;

            var soundObjects = fumen.GetAllDisplayableObjects().OfType<OngekiTimelineObjectBase>();

            foreach (var group in soundObjects.GroupBy(x => x.TGrid).OrderBy(x => x.Key))
            {
                var sounds = (Sound)0;

                foreach (var obj in group.DistinctBy(x => x.IDShortName))
                {
                    sounds = sounds | obj switch
                    {
                        WallTap { IsCritical: false } => Sound.WallExTap,
                        WallTap { IsCritical: true } => Sound.WallExTap,
                        Tap { IsCritical: false } or Hold { IsCritical: false } => Sound.Tap,
                        Tap { IsCritical: true } or Hold { IsCritical: true } => Sound.ExTap,
                        Bell => Sound.Bell,
                        Bullet => Sound.Bullet,
                        Flick { IsCritical: false } => Sound.Flick,
                        Flick { IsCritical: true } => Sound.ExFlick,
                        HoldEnd => Sound.HoldEnd,
                        _ => default
                    };
                }

                if (sounds != 0)
                {
                    var evt = ObjectPool<SoundEvent>.Get();
                    evt.Sounds = sounds;
                    evt.Time = TGridCalculator.ConvertTGridToY(group.Key, editor);
                    events.AddLast(evt);
                }
            }

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
                    var ct = CurrentTime - itor.Value.Time;
                    if (ct >= 0)
                    {
                        //Debug.WriteLine($"diff:{currentTime - itor.Value.Time:F2}ms/{currentTime - player.CurrentTime:F2}ms target:{itor.Value.Time:F2} {itor.Value.Sounds}");
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
                if (sounds.HasFlag(subFlag) && SoundControl.HasFlag(control))
                    cacheSounds[subFlag].PlayOnce();
            }

            checkPlay(Sound.Tap, SoundControl.Tap);
            checkPlay(Sound.ExTap, SoundControl.CriticalTap);
            checkPlay(Sound.Bell, SoundControl.Bell);
            checkPlay(Sound.Wall, SoundControl.WallTap);
            checkPlay(Sound.WallExTap, SoundControl.CriticalWallTap);
            checkPlay(Sound.Bullet, SoundControl.Bullet);
            checkPlay(Sound.Flick, SoundControl.Flick);
            checkPlay(Sound.ExFlick, SoundControl.CriticalFlick);
        }

        public void Seek(float msec, bool pause)
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
    }
}
