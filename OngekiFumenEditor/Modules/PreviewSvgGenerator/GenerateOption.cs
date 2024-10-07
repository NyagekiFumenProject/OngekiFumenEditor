using OngekiFumenEditor.Kernel.ArgProcesser.Attributes;
using System;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator
{
    public class GenerateOption
    {
        [LocalizableOptionBinding<string>("inputFile", "ProgramOptionInputFileNyageki", default, true)]
        public string InputFumenFilePath { get; set; }
        [LocalizableOptionBinding<string>("outputFile", "ProgramOptionOutputFile", default, true)]
        public string OutputFilePath { get; set; }
        
        [LocalizableOptionBinding<double>("maxXGrid", "ProgramOptionSvgMaxXGrid", 40)]
        public double XGridDisplayMaxUnit { get; set; } = 40;
        [LocalizableOptionBinding<double>("viewWidth", "ProgramOptionSvgViewWidth", 800)]
        public double ViewWidth { get; set; } = 800;
        [LocalizableOptionBinding<double>("verticalScale", "ProgramOptionSvgVerticalScale", 1)]
        public double VerticalScale { get; set; } = 1;

        [LocalizableOptionBinding<string>("audioFile", "ProgramOptionInputFileAudio", default)]
        public string AudioFilePath { get; set; }

        [LocalizableOptionBinding<SoflanMode>("soflanMode", "ProgramOptionSvgSoflanMode", SoflanMode.Soflan)]
        public SoflanMode SoflanMode { get; set; } = SoflanMode.WeightedSoflan;

        [LocalizableOptionBinding<float>("weightedSoflanOffset", "ProgramOptionSvgWeightedSoflanOffset", 70f)]
        public float WeightedSoflanOffset { get; set; } = 70f;
        [LocalizableOptionBinding<float>("weightedSoflanStress", "ProgramOptionSvgWeightedSoflanStress", 0.95f)]
        public float WeightedSoflanStress { get; set; } = 0.95f;
        [LocalizableOptionBinding<float>("weightedSoflanSlope", "ProgramOptionSvgWeightedSoflanSlope", 0.25f)]
        public float WeightedSoflanSlope { get; set; } = 0.25f;

        [LocalizableOptionBinding<bool>("png", "ProgramOptionSvgRenderAsPng", false)]
        public bool RenderAsPng { get; set; } = false;

        internal TimeSpan Duration { get; set; }
    }
}