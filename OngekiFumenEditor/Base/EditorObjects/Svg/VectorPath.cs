using OngekiFumenEditor.Base.ValueTypes;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.EditorObjects.Svg;

public class VectorPath
{
    public List<VectorPoint> Points { get; set; } = new();

    public Color Color { get; set; }

    public bool IsClosed { get; set; }
}
