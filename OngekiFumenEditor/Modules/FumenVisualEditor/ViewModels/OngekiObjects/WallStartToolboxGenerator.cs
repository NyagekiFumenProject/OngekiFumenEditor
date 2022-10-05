using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.OngekiObjects;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public class WallStartToolboxGenerator<T> : ToolboxGenerator<T> where T : WallStartBase, new()
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Wall Left Start", "Ongeki Lanes")]
    public class WallLeftStartToolboxGenerator : WallStartToolboxGenerator<WallLeftStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Wall Right Start", "Ongeki Lanes")]
    public class WallRightStartToolboxGenerator : WallStartToolboxGenerator<WallRightStart>
    {

    }
}
