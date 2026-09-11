using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Maintains front and back draw command list slots independently for each render context.
    /// </summary>
    public sealed class DrawCommandListContextSlots
    {
        private readonly Dictionary<IRenderContext, ContextSlot> contextSlots = new();

        /// <summary>
        /// Posts a command list into the back slot for the specified context.
        /// </summary>
        public void Post(IRenderContext context, DrawCommandList drawCommandList, bool autoDispose = true)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (drawCommandList is null)
                throw new ArgumentNullException(nameof(drawCommandList));

            DrawCommandListSlot? oldBack = null;

            var slot = GetOrCreateSlot(context);
            oldBack = slot.Back;
            slot.Back = new DrawCommandListSlot(drawCommandList, autoDispose);

            ReleaseSlot(oldBack);
        }

        /// <summary>
        /// Moves the back slot to the front slot for the specified context and releases the
        /// front slot it supersedes. The promoted list stays alive afterwards as the retained
        /// frame.
        /// </summary>
        public bool Swap(IRenderContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            DrawCommandListSlot? oldFront = null;
            var swapped = false;

            var slot = GetOrCreateSlot(context);
            if (slot.Back is not { } back)
                return false;

            oldFront = slot.Front;
            slot.Front = back;
            slot.Back = null;
            swapped = true;

            ReleaseSlot(oldFront);
            return swapped;
        }

        /// <summary>
        /// Presents the current front slot for the specified context and keeps it as the
        /// retained frame. The list is not released here: it stays re-presentable until it
        /// is superseded by <see cref="Swap"/> or the slot is dropped by <see cref="Remove"/>.
        /// </summary>
        public void Present(IRenderContext context, Action<DrawCommandList> presentCommands)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (presentCommands is null)
                throw new ArgumentNullException(nameof(presentCommands));

            if (!contextSlots.TryGetValue(context, out var slot))
                return;
            if (slot.Front is not { } value)
                return;

            var drawCommandList = value.DrawCommandList;
            if (!drawCommandList.TryBeginPresent())
                return;

            try
            {
                presentCommands(drawCommandList);
            }
            finally
            {
                drawCommandList.EndPresent();
            }
        }

        /// <summary>
        /// Removes and releases any queued command lists for the specified context.
        /// </summary>
        public bool Remove(IRenderContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (!contextSlots.Remove(context, out var slot))
                return false;

            ReleaseSlot(slot.Front);
            ReleaseSlot(slot.Back);
            return true;
        }

        private ContextSlot GetOrCreateSlot(IRenderContext context)
        {
            if (!contextSlots.TryGetValue(context, out var slot))
                slot = contextSlots[context] = new ContextSlot();

            return slot;
        }

        private static void ReleaseSlot(DrawCommandListSlot? slot)
        {
            if (slot is { AutoDispose: true } value)
                value.DrawCommandList.Dispose();
        }
    }
}
