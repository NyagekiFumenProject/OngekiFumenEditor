namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
{
    public sealed class ContextSlot
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
}
