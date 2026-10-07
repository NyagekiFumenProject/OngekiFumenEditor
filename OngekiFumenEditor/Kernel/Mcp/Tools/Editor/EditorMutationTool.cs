using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    [Export(typeof(EditorMutationTool))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed partial class EditorMutationTool
    {
        // lane / beam 是「可连接物件」：集合里只枚举起点（LaneStartBase / BeamStart），
        // 延伸段与曲线控制点分别由 lanenext / beamnext / curvecontrol 三个族单独暴露。
        private static readonly string[] QuerableFamilies =
        {
            "tap", "flick", "hold", "bell", "bullet", "comment", "bpm", "meter", "clickse", "enemy", "lane", "soflan",
            "lanenext", "beam", "beamnext", "curvecontrol", "isfarea", "laneblock",
        };

        private static readonly string[] CreatableFamilies = { "tap", "flick", "comment", "bpm", "bullet", "bell", "meter", "clickse", "enemy", "lane", "hold", "soflan", "lanenext", "beam", "beamnext", "curvecontrol", "isfarea", "laneblock" };

        private readonly IEditorDocumentManager editorDocumentManager;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;
        private readonly IEditorActionScopeManager actionScopeManager;

        [ImportingConstructor]
        public EditorMutationTool(IEditorDocumentManager editorDocumentManager, IMcpToolAuthorizationService mcpToolAuthorizationService, IEditorActionScopeManager actionScopeManager)
        {
            this.editorDocumentManager = editorDocumentManager;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
            this.actionScopeManager = actionScopeManager;
        }

        private static readonly string[] SupportedModifyProperties = { "tGridUnit", "tGridGrid", "xGridUnit", "xGridGrid", "isCritical", "direction", "content", "tag", "bpm", "bulletPallete", "shooter", "target", "size", "type", "bulletDamageType", "placeOffset", "randomOffsetRange", "bunShi", "bunbo", "enemyWave", "endTGridUnit", "endTGridGrid", "speed", "soflanGroup", "applySpeedInDesignMode", ReferenceLaneRecordIdProperty, "widthId", "obliqueSourceXGridUnit", "obliqueSourceXGridGrid", "colorId", "brightness", "endXGridUnit", "endXGridGrid", "blockDirection" };

        /// <summary>
        /// §42：bullet/bell 的 custom projectile 参数。palette 非空时这些属性在属性浏览器里只读，
        /// modify_object 同样要求先清掉 bulletPallete 才能写。
        /// </summary>
        private static readonly string[] ProjectileCustomProperties = { "shooter", "target", "size", "type", "bulletDamageType", "placeOffset", "randomOffsetRange", "speed" };

        // ---------------- bullet pallete (BPL) tools ----------------

        private const string BulletPalleteObjectType = "bullet_pallete";

        /// <summary>
        /// editor.modify_bullet_pallete 支持的属性，与 BulletPallete 的可写字段一致。
        /// 不含 StrID（改 id 会牵动所有引用，并触发 BulletPalleteList 的同 id 静默替换）与派生的 IsEnableSoflan。
        /// </summary>
        private static readonly string[] SupportedPalleteProperties = { "editorName", "shooter", "target", "size", "type", "speed", "placeOffset", "randomOffsetRange" };
    }
}
