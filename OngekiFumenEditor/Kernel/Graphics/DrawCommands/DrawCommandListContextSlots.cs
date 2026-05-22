using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
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
        /// Moves the back slot to the front slot for the specified context.
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
        /// Presents and clears the current front slot for the specified context.
        /// </summary>
        public void Present(IRenderContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            DrawCommandListSlot? front = null;

            if (!contextSlots.TryGetValue(context, out var slot))
                return;

            front = slot.Front;
            slot.Front = null;

            if (front is not { } value)
                return;

            var drawCommandList = value.DrawCommandList;
            try
            {
                if (!drawCommandList.TryBeginPresent())
                    return;

                try
                {
                    PresentCommands(drawCommandList);
                }
                finally
                {
                    drawCommandList.EndPresent();
                }
            }
            finally
            {
                if (value.AutoDispose)
                    drawCommandList.Dispose();
            }
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

        private static void PresentCommands(DrawCommandList drawCommandList)
        {
            foreach (var command in drawCommandList.Commands)
            {
                switch (command)
                {
                    case SetCurrentModelMatrixCommand:
                    case SetCurrentViewMatrixCommand:
                    case SetCurrentProjectionMatrixCommand:
                    case PushModelMatrixCommand:
                    case PushViewMatrixCommand:
                    case PushProjectionMatrixCommand:
                    case PopModelMatrixCommand:
                    case PopViewMatrixCommand:
                    case PopProjectionMatrixCommand:
                    case DrawLinesCommand:
                    case DrawSimpleLinesCommand:
                    case DrawTextureCommand:
                    case DrawBatchTextureCommand:
                    case DrawHighlightBatchTextureCommand:
                    case DrawCirclesCommand:
                    case DrawPolygonCommand:
                    case DrawStringCommand:
                    case DrawBeamCommand:
                        // TODO: Execute backend-specific draw command rendering here.
                        break;
                    default:
                        // TODO: Decide how custom draw commands should be dispatched.
                        break;
                }
            }
        }

        private sealed class ContextSlot
        {
            /// <summary>
            /// Gets or sets the slot currently ready for presentation.
            /// </summary>
            public DrawCommandListSlot? Front { get; set; }

            /// <summary>
            /// Gets or sets the slot waiting to be swapped to the front.
            /// </summary>
            public DrawCommandListSlot? Back { get; set; }
        }

        private readonly record struct DrawCommandListSlot(DrawCommandList DrawCommandList, bool AutoDispose);
    }
}
