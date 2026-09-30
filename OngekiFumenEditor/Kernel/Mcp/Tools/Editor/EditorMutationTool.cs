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
        private static readonly string[] QuerableFamilies =
        {
            "tap", "flick", "hold", "bell", "bullet", "comment", "bpm", "meter", "clickse", "enemy", "lane", "soflan",
        };

        private static readonly string[] CreatableFamilies = { "tap", "flick", "comment", "bpm", "bullet", "bell", "meter", "clickse", "enemy", "lane", "hold", "soflan" };

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

        private static readonly string[] SupportedModifyProperties = { "tGridUnit", "tGridGrid", "xGridUnit", "xGridGrid", "isCritical", "direction", "content", "bpm", "bulletPallete", "bunShi", "bunbo", "enemyWave", "endTGridUnit", "endTGridGrid", "speed", "soflanGroup", "applySpeedInDesignMode", ReferenceLaneRecordIdProperty };

        // ---------------- bullet pallete (BPL) tools ----------------

        private const string BulletPalleteObjectType = "bullet_pallete";

        /// <summary>
        /// editor.modify_bullet_pallete 支持的属性，与 BulletPallete 的可写字段一致。
        /// 不含 StrID（改 id 会牵动所有引用，并触发 BulletPalleteList 的同 id 静默替换）与派生的 IsEnableSoflan。
        /// </summary>
        private static readonly string[] SupportedPalleteProperties = { "editorName", "shooter", "target", "size", "type", "speed", "placeOffset", "randomOffsetRange" };
    }
}
