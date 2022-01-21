using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.UI.Controls
{
    public class AnimationWrapper
    {
        private AnimationTimeline animation = default;
        private FrameworkElement frameworkElement;

        private AnimationClock clock;
        private readonly DependencyProperty dp;

        private ClockController Controller => clock.Controller;

        public AnimationWrapper(AnimationTimeline animation, FrameworkElement frameworkElement, DependencyProperty dp)
        {
            this.animation = animation;
            this.frameworkElement = frameworkElement;
            this.dp = dp;
        }

        private void CheckController()
        {
            if (clock is not null)
                return;
            clock = animation.CreateClock();
            frameworkElement.ApplyAnimationClock(dp, clock);
        }

        public virtual void Start()
        {
            CheckController();
            Controller.Begin();
        }

        public virtual void Resume()
        {
            CheckController();
            Controller.Resume();
        }

        public virtual void Stop()
        {
            CheckController();
            Controller.Stop();
        }

        public virtual void Pause()
        {
            CheckController();
            Controller.Pause();
        }

        public void JumpAndPause(TimeSpan offset, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
        {
            CheckController();
            Pause();
            Jump(offset, origin);
        }

        public virtual void Jump(TimeSpan offset, TimeSeekOrigin origin = TimeSeekOrigin.BeginTime)
        {
            CheckController();
            Controller.Seek(offset, origin);
        }
    }
}
