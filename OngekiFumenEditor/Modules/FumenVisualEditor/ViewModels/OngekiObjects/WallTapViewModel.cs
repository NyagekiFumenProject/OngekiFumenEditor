using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using Gemini.Modules.Toolbox;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Utils.Attributes;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.OngekiObjects;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Wall Tap", "Ongeki Objects")]
    [MapToView(ViewType = typeof(WallTapView))]
    public class WallTapViewModel : LaneDockableViewModelBase<WallTap>
    {
        public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = default)
        {
            return base.PickDockableObjects(editor)
                    .FilterNull()
                    .Where(x => x.IDShortName[0] == 'W');
        }
    }
}
