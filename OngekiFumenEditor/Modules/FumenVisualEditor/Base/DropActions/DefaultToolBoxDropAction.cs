using Gemini.Modules.Toolbox.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions
{
    public class DefaultToolBoxDropAction : EditorAddObjectDropAction
    {
        private readonly Type itemType;

        public DefaultToolBoxDropAction(ToolboxItem toolboxItem)
        {
            itemType = toolboxItem.ItemType;
        }

        protected override DisplayObjectViewModelBase GetDisplayObject()
        {
            return CacheLambdaActivator.CreateInstance(itemType) as DisplayObjectViewModelBase;
        }
    }
}
