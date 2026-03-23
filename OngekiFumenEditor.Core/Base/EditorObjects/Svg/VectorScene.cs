using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Base.EditorObjects.Svg;

public class VectorScene
{
    public VectorRect Bounds { get; set; }

    public List<VectorPath> Paths { get; set; } = new();
}
