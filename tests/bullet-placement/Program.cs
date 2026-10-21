using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System.Numerics;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Base;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            Run();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Environment.ExitCode = 1;
        }
    }

    static void Run()
    {
        // 不创建窗口、不启动编辑器，仅检验取消状态与输入保护。
        var type = typeof(FumenVisualEditorViewModel);
        var input = type.GetMethod("IsQuickPlacementTextInput", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var control in new object[]
        {
            new TextBox(),
            new PasswordBox(),
            new ComboBox()
        })
            Assert((bool)input.Invoke(null, new[] { control })!, "文本输入控件必须保留方向键。");
        Assert(!(bool)input.Invoke(null, new object[] { new object() })!, "非输入对象不应被误判。");

        var editor = (FumenVisualEditorViewModel)RuntimeHelpers.GetUninitializedObject(type);
        var initializedViewAware = new Caliburn.Micro.Screen();
        for (var inherited = typeof(Caliburn.Micro.Screen); inherited != null; inherited = inherited.BaseType)
            foreach (var field in inherited.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                field.SetValue(editor, field.GetValue(initializedViewAware));
        var palette = new BulletPallete();
        var preview = new Bullet
        {
            ReferenceBulletPallete = palette
        };
        void Field(string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(editor, value);
        Field("quickPlacementPreviewObject", preview);
        Field("quickPlacementFactory", (Func<OngekiMovableObjectBase>)(() => new Bullet()));
        Field("quickPlacingDrag", true);
        var cancelled = 0;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(editor.QuickPlacementPalette))
                cancelled++;
        };
        editor.CancelQuickPlacement();
        Assert(!editor.IsQuickPlacementActive, "取消必须清除放置工厂。");
        Assert(preview.ReferenceBulletPallete == null, "取消必须解除预览与调色板的事件引用。");
        Assert(type.GetField("quickPlacementPreviewObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor) == null, "取消必须释放预览。");
        editor.CancelQuickPlacement();
        Assert(cancelled == 2, "重复取消必须安全并同步勾选状态。");

        Field("editorProjectData", new EditorProjectDataModel());
        var fumen = editor.Fumen;
        fumen.AddObject(palette);
        preview = new Bullet
        {
            ReferenceBulletPallete = palette
        };
        Field("quickPlacementPreviewObject", preview);
        Field("quickPlacementFactory", (Func<OngekiMovableObjectBase>)(() => new Bullet()));
        Field("quickPlacementFumen", fumen);
        var paletteChanged = type.GetMethod("OnQuickPlacementPalettesChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        fumen.BulletPalleteList.CollectionChanged += (System.Collections.Specialized.NotifyCollectionChangedEventHandler)
            paletteChanged.CreateDelegate(typeof(System.Collections.Specialized.NotifyCollectionChangedEventHandler), editor);
        fumen.RemoveObject(palette);
        Assert(!editor.IsQuickPlacementActive && preview.ReferenceBulletPallete == null,
            "删除或撤回新建调色板必须立即取消放置并解除预览引用。");
        Field("quickPlacementPreviewObject", new Bullet
        {
            ReferenceBulletPallete = palette
        });
        Field("quickPlacementFactory", (Func<OngekiMovableObjectBase>)(() => throw new InvalidOperationException("不得执行悬空工厂")));
        type.GetMethod("PlaceQuickObject", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, new object[] { new System.Windows.Point() });
        Assert(!editor.IsQuickPlacementActive, "放置时必须再次拒绝不存在的调色板。");
        typeof(OngekiFumenEditor.Utils.Log).GetField("cacheInstance", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, new OngekiFumenEditor.Utils.Log(Array.Empty<OngekiFumenEditor.Utils.Logs.ILogOutput>()));
        var rightFlag = type.GetField("consumeQuickRightMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        rightFlag.SetValue(editor, true);
        var rightRelease = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 1, System.Windows.Input.MouseButton.Right)
        {
            RoutedEvent = System.Windows.Input.Mouse.PreviewMouseUpEvent
        };
        editor.OnMouseUp(new Caliburn.Micro.ActionExecutionContext
        {
            EventArgs = rightRelease
        });
        Assert(rightRelease.Handled && !(bool)rightFlag.GetValue(editor)!, "取消后的右键释放必须单次消费并清状态。");
        var ordinaryRelease = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 2, System.Windows.Input.MouseButton.Right)
        {
            RoutedEvent = System.Windows.Input.Mouse.PreviewMouseUpEvent
        };
        editor.OnMouseUp(new Caliburn.Micro.ActionExecutionContext
        {
            EventArgs = ordinaryRelease
        });
        Assert(!ordinaryRelease.Handled, "普通右键释放不应被吞。");
        // 使用生产放置入口与真实撤销管理器，验证临时伤害不污染模板/谱面。
        Field("SelectionArea", new SelectionArea(editor));
        Field("renderDataLock", new System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.SupportsRecursion));
        var setting = new EditorSetting();
        void SettingField(string name, object value) => typeof(EditorSetting).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(setting, value);
        SettingField("disableXGridMagneticDock", true);
        SettingField("disableTGridMagneticDock", true);
        SettingField("forceXGridIntegerUnit", true);
        SettingField("forceMagneticDock", false);
        SettingField("xGridDisplayMaxUnit", 10);
        SettingField("xGridUnitSpace", 1d);
        SettingField("xOffset", 0d);
        SettingField("verticalDisplayScale", 1d);
        Field("<Setting>k__BackingField", setting);
        Field("<InteractiveManager>k__BackingField", new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.ObjectInteractiveManager());
        editor.ViewWidth = 1000;
        editor.RectInDesignMode = new VisibleRect(new Vector2(1000, 0), new Vector2(0, 1000));
        var undo = new OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl.DefaultEditorUndoManager(editor);
        typeof(OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl.DefaultEditorUndoManager).GetField("isRecoveryCurrentTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(undo, false);
        undo.UndoCountLimit = null;
        typeof(Gemini.Framework.PersistedDocument).GetProperty("UndoRedoManager")!.SetValue(editor, undo);
        var selected = new Bullet { IsSelected = true };
        fumen.AddObject(selected);
        fumen.AddObject(palette);
        editor.BeginPaletteQuickPlacement(palette, false);
        Assert(editor.IsQuickPlacementActive && editor.QuickPlacementPalette == palette, "生产入口必须建立模板绑定状态。");
        preview = (Bullet)type.GetField("quickPlacementPreviewObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
        var context = new Caliburn.Micro.ActionExecutionContext();
        editor.KeyboardAction_QuickBulletDamageUp(context);
        Assert(preview.BulletDamageTypeValue == BulletDamageType.Hard && selected.BulletDamageTypeValue == BulletDamageType.Normal,
            "临时伤害只修改吸附预览，不能修改已选谱面子弹。");
        editor.KeyboardAction_QuickBulletDamageUp(context);
        Assert(preview.BulletDamageTypeValue == BulletDamageType.Danger, "上键必须循环强化到危险。");
        editor.KeyboardAction_QuickBulletDamageUp(context);
        Assert(preview.BulletDamageTypeValue == BulletDamageType.Normal, "上键必须从危险回到普通。");
        editor.KeyboardAction_QuickBulletDamageDown(context);
        Assert(preview.BulletDamageTypeValue == BulletDamageType.Danger && undo.UndoActionCount == 0,
            "下键反向循环，临时状态不得进入谱面撤销栈。");
        editor.BeginPaletteQuickPlacement(palette, false);
        preview = (Bullet)type.GetField("quickPlacementPreviewObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
        Assert(preview.BulletDamageTypeValue == BulletDamageType.Normal, "重新选模板必须恢复普通伤害。");
        var modelMutations = 0;
        fumen.ObjectModifiedChanged += (_, _) => modelMutations++;
        editor.CurrentCursorPosition = new System.Windows.Point(561, 100);
        type.GetMethod("UpdateQuickPlacementPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
        var positioned = new Bullet();
        editor.MoveObjectTo(positioned, editor.CurrentCursorPosition.Value);
        Assert(preview.XGrid == positioned.XGrid && preview.TGrid == positioned.TGrid,
            "鼠标预览必须与正式交互定位一致，并遵守整数 X 吸附。");
        Assert(preview.XGrid.Unit == 1 && preview.XGrid.Grid == 0, "预览必须复用 ForceXGridIntegerUnit。");
        editor.KeyboardAction_QuickBulletDamageUp(context);
        Assert(editor.QuickPlacementImage.EndsWith("bulletCircleHard.png"), "伤害切换必须立即更新鼠标图像。");
        Assert(modelMutations == 0 && selected.IsSelected,
            "移动预览与临时伤害不得修改谱面对象或全局选中状态。");
        editor.CurrentCursorPosition = null;
        editor.CancelQuickPlacement();
        editor.KeyboardAction_QuickBulletDamageUp(context);
        Assert(selected.BulletDamageTypeValue == BulletDamageType.Hard && undo.UndoActionCount == 1,
            "无放置会话时，上键修改选中子弹并记录撤销。");
        undo.Undo(1);
        Assert(selected.BulletDamageTypeValue == BulletDamageType.Normal, "伤害修改必须可撤销。");
        undo.Redo(1);
        Assert(selected.BulletDamageTypeValue == BulletDamageType.Hard, "伤害修改必须可重做。");
        var converter = new OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.ValueConverters.QuickPlacementSelectionConverter();
        Assert((bool)converter.Convert(new object[] { palette, palette, false }, typeof(bool), "Bullet", System.Globalization.CultureInfo.InvariantCulture),
            "当前子弹模板应勾选。");
        Assert(!(bool)converter.Convert(new object[] { palette, palette, false }, typeof(bool), "Bell", System.Globalization.CultureInfo.InvariantCulture),
            "同模板的 Bell 不应同时勾选。");
        var previousGetInstance = Caliburn.Micro.IoC.GetInstance;
        var services = new Dictionary<Type, object>();
        Caliburn.Micro.IoC.GetInstance = (service, key) =>
        {
            if (!services.TryGetValue(service, out var instance))
                services.Add(service, instance = DispatchProxy.Create(service, typeof(NullServiceProxy)));
            return instance;
        };
        try
        {
            var paletteViewerType = typeof(OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.ViewModels.FumenBulletPalleteListViewerViewModel);
            var paletteViewer = (OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.ViewModels.FumenBulletPalleteListViewerViewModel)RuntimeHelpers.GetUninitializedObject(paletteViewerType);
            paletteViewerType.GetField("editor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(paletteViewer, editor);
            editor.BeginPaletteQuickPlacement(palette, false);
            var temporary = (Bullet)type.GetField("quickPlacementPreviewObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
            var paletteKey = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                new NoWindowPresentationSource(), 0, System.Windows.Input.Key.Up)
            {
                RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent,
                Source = new CheckBox()
            };
            paletteViewer.KeyboardAction_QuickBulletDamageUp(new Caliburn.Micro.ActionExecutionContext { EventArgs = paletteKey });
            Assert(paletteKey.Handled, "模板行必须消费临时伤害按键，防止 ListView 导航与根绑定重复处理。");
            Assert(temporary.BulletDamageTypeValue == BulletDamageType.Hard,
                "焦点留在调色板时，调色板快捷键动作必须转发到当前编辑器的临时子弹。");
            paletteViewer.KeyboardAction_QuickBulletDamageDown(context);
            Assert(temporary.BulletDamageTypeValue == BulletDamageType.Normal, "调色板必须支持向下反向循环。");
            foreach (var inputControl in new System.Windows.UIElement[] { new TextBox(), new PasswordBox(), new ComboBox() })
            {
                var inputKey = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    new NoWindowPresentationSource(), 0, System.Windows.Input.Key.Up)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent,
                    Source = inputControl
                };
                paletteViewer.KeyboardAction_QuickBulletDamageUp(new Caliburn.Micro.ActionExecutionContext { EventArgs = inputKey });
                Assert(temporary.BulletDamageTypeValue == BulletDamageType.Normal,
                    "调色板中的文本/密码/组合框原始事件不得修改临时伤害。");
            }
            editor.CancelQuickPlacement();
            var selectedDamage = selected.BulletDamageTypeValue;
            paletteViewer.KeyboardAction_QuickBulletDamageUp(context);
            Assert(selected.BulletDamageTypeValue == selectedDamage,
                "调色板没有吸附会话时不得修改谱面已选子弹。");
            undo.Clear();
            editor.TotalDurationHeight = 1000;
            editor.BeginPaletteQuickPlacement(palette, false);
            editor.KeyboardAction_QuickBulletDamageDown(context);
            var placement = type.GetMethod("PlaceQuickObject", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var count = fumen.Bullets.Count;
            placement.Invoke(editor, new object[] { new System.Windows.Point(561, 100) });
            placement.Invoke(editor, new object[] { new System.Windows.Point(661, 200) });
            var placed = fumen.Bullets.Where(x => x != selected).ToArray();
            Assert(fumen.Bullets.Count == count + 2 && editor.IsQuickPlacementActive,
                "生产 Drop 入口必须连续添加独立子弹并保持放置会话。");
            Assert(placed.All(x => x.BulletDamageTypeValue == BulletDamageType.Danger && x.ReferenceBulletPallete == palette),
                "Drop 工厂必须复制临时伤害并保留模板引用。");
            Assert(placed[0].XGrid != placed[1].XGrid && placed[0].TGrid != placed[1].TGrid,
                "连续放置必须分别使用各次点击坐标。");
            undo.Undo(1);
            Assert(fumen.Bullets.Count == count + 1 && editor.IsQuickPlacementActive,
                "撤销必须移除最后一个子弹且不结束持续放置。");
            undo.Redo(1);
            Assert(fumen.Bullets.Count == count + 2 && placed.All(x => x.BulletDamageTypeValue == BulletDamageType.Danger),
                "重做必须恢复原对象和临时伤害。");
            var originalDefaults = OngekiFumenEditor.Properties.EditorGlobalSetting.Default;
            var defaultsField = typeof(OngekiFumenEditor.Properties.EditorGlobalSetting).GetField("defaultInstance", BindingFlags.Static | BindingFlags.NonPublic)!;
            var isolatedDefaults = new OngekiFumenEditor.Properties.EditorGlobalSetting
            {
                EnablePlaceObjectBeyondAudioDuration = false
            };
            var originalFactory = type.GetField("quickPlacementFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor);
            var factoryCalls = 0;
            Field("quickPlacementFactory", (Func<OngekiMovableObjectBase>)(() =>
            {
                factoryCalls++;
                return new Bullet { ReferenceBulletPallete = palette };
            }));
            defaultsField.SetValue(null, isolatedDefaults);
            try
            {
                var undoCount = undo.UndoActionCount;
                for (var invalidClick = 0; invalidClick < 5; invalidClick++)
                    placement.Invoke(editor, new object[] { new System.Windows.Point(561, -1) });
                Assert(factoryCalls == 0 && undo.UndoActionCount == undoCount && fumen.Bullets.Count == count + 2,
                    "重复越界必须在构造引用模板对象之前拒绝，不得增加引用或撤销动作。");
            }
            finally
            {
                defaultsField.SetValue(null, originalDefaults);
                Field("quickPlacementFactory", originalFactory!);
            }
            var deactivate = type.GetMethod("OnDeactivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((Task)deactivate.Invoke(editor, new object[] { false, System.Threading.CancellationToken.None })!).GetAwaiter().GetResult();
            Assert(!editor.IsQuickPlacementActive && editor.QuickPlacementPalette == null,
                "编辑器停用生命周期必须取消会话并清除绑定状态。");
            editor.BeginPaletteQuickPlacement(palette, false);
            type.GetProperty("IsUserRequestHideEditorObject", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor, true);
            Assert(!editor.IsQuickPlacementActive, "设计/预览模式切换必须立即取消。");
        }
        finally
        {
            Caliburn.Micro.IoC.GetInstance = previousGetInstance;
        }
        undo.Dispose();
        setting.Dispose();
        Console.WriteLine("子弹放置无窗口回归通过。");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

public class NullServiceProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method!.ReturnType == typeof(Task) ? Task.CompletedTask : null;
}

public class NoWindowPresentationSource : System.Windows.PresentationSource
{
    public override System.Windows.Media.Visual RootVisual { get; set; } = null!;
    public override bool IsDisposed => false;
    protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore() => null!;
}
