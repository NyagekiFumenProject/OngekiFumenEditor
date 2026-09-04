using System;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Measures strings with a backend text renderer without producing any draw command.
    /// </summary>
    public interface IStringMeasure
    {
        System.Numerics.Vector2 MeasureString(string text, System.Numerics.Vector2 scale, int fontSize, IStringDrawing.StringStyle style, IStringDrawing.IFontHandle handle);
    }
}
