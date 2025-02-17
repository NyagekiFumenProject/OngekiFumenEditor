using OngekiFumenEditor.Kernel.CommandExecutor.Attributes;
using OngekiFumenEditor.Properties;
using System;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator
{
    public class SvgGenerateOption
    {
        [LocalizableOptionBinding<string>("inputFile", nameof(Resources.ProgramOptionInputFileNyageki), default, true)]
        public string InputFumenFilePath { get; set; }
        [LocalizableOptionBinding<string>("outputFile", nameof(Resources.ProgramOptionOutputFile), default, true)]
        public string OutputFilePath { get; set; }
        [LocalizableOptionBinding<string>("audioFile", nameof(Resources.ProgramOptionInputFileAudio), default, true)]
        public string AudioFilePath { get; set; }

        [LocalizableOptionBinding<double>("maxXGrid", nameof(Resources.ProgramOptionSvgMaxXGrid), 40)]
        public double XGridDisplayMaxUnit { get; set; } = 40;
        [LocalizableOptionBinding<double>("viewWidth", nameof(Resources.ProgramOptionSvgViewWidth), 800)]
        public double ViewWidth { get; set; } = 800;
        [LocalizableOptionBinding<double>("verticalScale", nameof(Resources.ProgramOptionSvgVerticalScale), 1)]
        public double VerticalScale { get; set; } = 1;

        [LocalizableOptionBinding<SoflanMode>("soflanMode", nameof(Resources.ProgramOptionSvgSoflanMode), SoflanMode.Soflan)]
        public SoflanMode SoflanMode { get; set; } = SoflanMode.WeightedSoflan;
        [LocalizableOptionBinding<float>("weightedSoflanOffset", nameof(Resources.ProgramOptionSvgWeightedSoflanOffset), 70f)]
        public float WeightedSoflanOffset { get; set; } = 70f;
        [LocalizableOptionBinding<float>("weightedSoflanStress", nameof(Resources.ProgramOptionSvgWeightedSoflanStress), 0.95f)]
        public float WeightedSoflanStress { get; set; } = 0.95f;
        [LocalizableOptionBinding<float>("weightedSoflanSlope", nameof(Resources.ProgramOptionSvgWeightedSoflanSlope), 0.25f)]
        public float WeightedSoflanSlope { get; set; } = 0.25f;

        [LocalizableOptionBinding<bool>("png", nameof(Resources.ProgramOptionSvgRenderAsPng), false)]
        public bool RenderAsPng { get; set; } = false;

        internal TimeSpan Duration { get; set; }
    }
}