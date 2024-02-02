using OngekiFumenEditor.Kernel.ArgProcesser.Attributes;
using System;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator
{
    public class GenerateOption
    {
        [OptionBindingAttrbute<double>("maxXGrid", "最大显示XGrid范围", 40)]
        public double XGridDisplayMaxUnit { get; set; } = 40;
        [OptionBindingAttrbute<double>("viewWidth", "画面宽度", 800)]
        public double ViewWidth { get; set; } = 800;
        [OptionBindingAttrbute<double>("verticalScale", "垂直缩放", 1)]
        public double VerticalScale { get; set; } = 1;

        [OptionBindingAttrbute<string>("inputFile", "要生成的谱面文件路径", default, Require = true)]
        public string InputFumenFilePath { get; set; }
        [OptionBindingAttrbute<string>("outputFile", "生成文件保存路径", default, Require = true)]
        public string OutputFilePath { get; set; }
        [OptionBindingAttrbute<string>("audioFile", "音频文件,用来获取谱面总长度", default)]
        public string AudioFilePath { get; set; }

        public TimeSpan Duration { get; set; }
    }
}