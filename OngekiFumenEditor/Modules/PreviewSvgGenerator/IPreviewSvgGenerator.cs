using Microsoft.CodeAnalysis.Options;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator
{
    public interface IPreviewSvgGenerator
    {
        Task<byte[]> GenerateSvgAsync(OngekiFumen fumen, SvgGenerateOption option);
    }
}
