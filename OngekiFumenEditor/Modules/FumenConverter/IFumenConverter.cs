using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenConverter
{
    public interface IFumenConverter : IWindow
    {
        Task ConvertFumenFile(string fromFilePath, string toFilePath);
    }
}
