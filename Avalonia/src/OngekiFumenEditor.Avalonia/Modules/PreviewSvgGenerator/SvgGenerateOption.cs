namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;

public class SvgGenerateOption
{
    public string InputFumenFilePath { get; set; }
    public string OutputFilePath { get; set; }
    public string AudioFilePath { get; set; }

    public double XGridDisplayMaxUnit { get; set; } = 40;
    public double ViewWidth { get; set; } = 800;
    public double VerticalScale { get; set; } = 1;

    public SoflanMode SoflanMode { get; set; } = SoflanMode.Soflan;

    public bool RenderAsPng { get; set; } = false;

    internal TimeSpan Duration { get; set; }
}

