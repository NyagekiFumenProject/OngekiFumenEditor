using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    [Export(typeof(IFumenSoundPlayer))]
    public class DefaultFumenSoundPlayer : IFumenSoundPlayer, IDisposable
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
            ExFlick = 512
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
        private Stopwatch timer = new Stopwatch();
        private FumenVisualEditorViewModel editor;
        private float baseTimeOffset = 0;
        private bool isPlaying = false;

        public double CurrentTime => baseTimeOffset + timer.ElapsedMilliseconds + editor.Setting.SoundOffset;

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

            async Task load(Sound sound, string filePath)
            {
                cacheSounds[sound] = await audioManager.LoadSoundAsync(filePath);
            }

            await load(Sound.Tap, "Resources\\sounds\\tap.wav");
            await load(Sound.Bell, "Resources\\sounds\\bell.wav");
            await load(Sound.ExTap, "Resources\\sounds\\extap.wav");
            await load(Sound.Wall, "Resources\\sounds\\wall.wav");
            await load(Sound.WallExTap, "Resources\\sounds\\exwall.wav");
            await load(Sound.Flick, "Resources\\sounds\\flick.wav");
            await load(Sound.Bullet, "Resources\\sounds\\bullet.wav");
            await load(Sound.ExFlick, "Resources\\sounds\\exflick.wav");

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
                var evt = ObjectPool<SoundEvent>.Get();

                foreach (var obj in group.DistinctBy(x => x.IDShortName))
                {
                    sounds = sounds | obj switch
                    {
                        WallTap { IsCritical: false } => Sound.WallExTap,
                        WallTap { IsCritical: true } => Sound.WallExTap,
                        Tap { IsCritical: false } => Sound.Tap,
                        Tap { IsCritical: true } => Sound.ExTap,
                        Bell => Sound.Bell,
                        Bullet => Sound.Bullet,
                        Flick { IsCritical: false } => Sound.Flick,
                        Flick { IsCritical: true } => Sound.ExFlick,
                        Hold => Sound.Tap,
                        HoldEnd => Sound.Tap,
                        _ => default
                    };
                }

                evt.Sounds = sounds;
                evt.Time = TGridCalculator.ConvertTGridToY(group.Key, editor);

                events.AddLast(evt);
            }

            itor = events.First;
        }

        private void OnUpdate(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                Thread.Yield();
                if (itor is null || player is null || !isPlaying)
                    continue;

                var currentTime = CurrentTime;
                while (itor is not null)
                {
                    if (currentTime >= itor.Value.Time)
                    {
                        //Debug.WriteLine($"diff:{currentTime - itor.Value.Time:F2}ms/{currentTime - player.CurrentTime:F2}ms target:{itor.Value.Time:F2} {itor.Value.Sounds}");
                        PlaySounds(itor.Value.Sounds);
                        itor = itor.Next;
                    }
                    else
                        break;
                }
            }
        }

        private void PlaySounds(Sound sounds)
        {
            void checkPlay(Sound subFlag)
            {
                if (sounds.HasFlag(subFlag))
                    cacheSounds[subFlag].PlayOnce();
            }

            checkPlay(Sound.Tap);
            checkPlay(Sound.ExTap);
            checkPlay(Sound.Bell);
            checkPlay(Sound.Wall);
            checkPlay(Sound.WallExTap);
            checkPlay(Sound.Bullet);
            checkPlay(Sound.Flick);
            checkPlay(Sound.ExFlick);
        }

        public void Seek(float msec)
        {
            Pause();
            itor = events.Find(events.FirstOrDefault(x => msec < x.Time));
            timer.Restart();
            baseTimeOffset = msec;
            Play();
        }

        public void Stop()
        {
            thread?.Abort();
            isPlaying = false;
        }

        public void Play()
        {
            timer.Restart();
            baseTimeOffset = player.CurrentTime;
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
