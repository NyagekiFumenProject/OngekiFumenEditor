using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public interface IFumenVisualEditorExtraMenuItemHandler
    {
        public const string COMMON_EXT_MENUITEM_ROOT = "插件...";

        /// <summary>
        /// e.g new []{"脚本","自定义...","打开我的脚本"}
        /// </summary>
        string[] RegisterMenuPath { get; }
        void Handle(FumenVisualEditorViewModel editor, EventArgs args);
    }
}
