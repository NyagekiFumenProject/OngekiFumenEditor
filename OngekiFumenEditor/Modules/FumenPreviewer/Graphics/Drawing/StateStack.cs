using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public class StateStack
    {
        private readonly Func<IEnumerable<int>> getter;
        private readonly Action<IEnumerable<int>> setter;

        private Stack<IEnumerable<int>> stacks = new();

        public StateStack(Func<IEnumerable<int>> getter, Action<IEnumerable<int>> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        public void PushState()
        {
            stacks.Push(getter());
        }

        public void PopState()
        {
            setter(stacks.Pop());
        }
    }
}
