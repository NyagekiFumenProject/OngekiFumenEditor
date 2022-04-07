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

        private RangeValue rotation = RangeValue.Create(0, 360f, 0f);
        public RangeValue Rotation
        {
            get => rotation;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(rotation, value);
                Set(ref rotation, value);
            }
        }

        private float scale = 1;
        public float Scale
        {
            get => scale;
            set => Set(ref scale, value);
        }

        private RangeValue opacity = RangeValue.CreateNormalized();
        public RangeValue Opacity
        {
            get => opacity;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(opacity, value);
                Set(ref opacity, value);
            }
        }

        private RangeValue tolerance = RangeValue.Create(0, 20f, 1f);
        public RangeValue Tolerance
        {
            get => tolerance;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(tolerance, value);
                Set(ref tolerance, value);
            }
        }

        private FileInfo svgFile = null;
        public FileInfo SvgFile
        {
            get => svgFile;
            set => Set(ref svgFile, value);
        }

        public SvgPrefab()
        {
            Tolerance = Tolerance;
            Opacity = Opacity;
            Rotation = Rotation;
        }

        public override string ToString() => $"{base.ToString()} R:∠{Rotation}° O:{Opacity.ValuePercent * 100:F2}% S:{Rotation:F2}x File:{Path.GetFileName(SvgFile?.Name)}";
    }
}
