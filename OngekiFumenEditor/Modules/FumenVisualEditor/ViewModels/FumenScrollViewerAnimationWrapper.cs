using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public class FumenScrollViewerAnimationWrapper : PropertyChangedBase, IAudioPlayer
    {
        private IAudioPlayer wrapCore = default;
        public IAudioPlayer WrapCore
        {
            get => wrapCore;
            set => Set(ref wrapCore, value);
        }

        private FumenVisualEditorViewModel editor = default;
        public FumenVisualEditorViewModel Editor
        {
            get => editor;
            set => Set(ref editor, value);
        }

        private AnimationWrapper animation = default;
        public AnimationWrapper Animation
        {
            get => animation;
            set => Set(ref animation, value);
        }

        public float CurrentTime => WrapCore.CurrentTime;

        public float Volume { get => WrapCore.Volume; set => WrapCore.Volume = value; }

        public float Duration => WrapCore.Duration;

        public bool IsPlaying => WrapCore.IsPlaying;

        public FumenScrollViewerAnimationWrapper(FumenVisualEditorViewModel editor = default, AnimationWrapper animation = default, IAudioPlayer core = default)
        {
            Animation = animation;
            Editor = editor;
            WrapCore = core;
        }

        public void Dispose()
        {
            Stop();
            WrapCore.Dispose();
        }

        public void Jump(float time, bool pause)
        {
            WrapCore.Jump(time, pause);
            Animation.JumpAndPause(TimeSpan.FromMilliseconds(time));
        }

        public void Pause()
        {
            WrapCore.Pause();
            Animation.Pause();
        }

        public void Play()
        {
            Editor.LockAllUserInteraction();
            Animation.JumpAndPause(TimeSpan.FromMilliseconds(WrapCore.CurrentTime));
            WrapCore.Play();
            Animation.Resume();
        }

        public void Stop()
        {
            Editor.UnlockAllUserInteraction();
            WrapCore.Stop();
            Animation.Stop();
            //todo
        }
    }
}
