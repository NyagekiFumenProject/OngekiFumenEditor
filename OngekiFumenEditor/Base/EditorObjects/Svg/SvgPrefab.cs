using OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
    public class SvgPrefab : OngekiMovableObjectBase
    {
        public override string IDShortName => "SVG";
        public override Type ModelViewType => typeof(SvgPrefabViewModel);

        private float rotation = 0;
        public float Rotation
        {
            get => rotation;
            set => Set(ref rotation, value);
        }

        private float scale = 0;
        public float Scale
        {
            get => scale;
            set => Set(ref scale, value);
        }

        private float opacity = 1;
        public float Opacity
        {
            get => opacity;
            set => Set(ref opacity, value);
        }

        private float tolerance = 0.1f;
        public float Tolerance
        {
            get => tolerance;
            set => Set(ref tolerance, value);
        }

        private string svgFilePath = "";
        public string SvgFilePath
        {
            get => svgFilePath;
            set => Set(ref svgFilePath, value);
        }

        public override string ToString() => $"{base.ToString()} R:∠{Rotation}° O:{Opacity * 100:F2}% S:{Rotation:F2}x File:{Path.GetFileName(SvgFilePath)}";
    }
}
