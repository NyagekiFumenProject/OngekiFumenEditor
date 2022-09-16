using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels;
using OngekiFumenEditor.Utils;
using SharpVectors.Renderers.Wpf;
using SvgConverter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
    public class SvgImageFilePrefab : SvgPrefabBase
    {
        public const string CommandName = "[SVG_IMG]";
        public override string IDShortName => CommandName;
        public override Type ModelViewType => typeof(SvgImageFilePrefabViewModel);

        private FileInfo svgFile = null;
        public FileInfo SvgFile
        {
            get => svgFile;
            set => Set(ref svgFile, value);
        }

        public SvgImageFilePrefab() : base()
        {

        }

        public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            switch (propertyName)
            {
                case nameof(SvgFile):
                    ReloadSvgFile();
                    break;
                default:
                    base.NotifyOfPropertyChange(propertyName);
                    break;
            }
        }

        public void ReloadSvgFile()
        {
            if (SvgFile is null)
                return;

            var svgContent = ConverterLogic.ConvertSvgToObject(SvgFile.FullName, ResultMode.DrawingGroup, new WpfDrawingSettings()
            {
                IncludeRuntime = false,
                TextAsGeometry = true,
                OptimizePath = true,
                EnsureViewboxSize = true
            }, out _, new()) as DrawingGroup;
            svgContent.Freeze();

            RebuildGeometry();

            ApplySvgContent(svgContent);
        }

        public override string ToString() => $"{base.ToString()} File:{Path.GetFileName(SvgFile?.Name)}";
    }
}
