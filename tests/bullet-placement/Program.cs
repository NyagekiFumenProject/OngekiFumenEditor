using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System.Numerics;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects;
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
        // 不创建窗口、不启动编辑器，仅检验取消状态与输入保护。
        var type = typeof(FumenVisualEditorViewModel);
        var input = type.GetMethod("IsQuickPlacementTextInput", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var control in new object[] { new TextBox(), new PasswordBox(), new ComboBox() })
            Assert((bool)input.Invoke(null, new[] { control })!, "文本输入控件必须保留方向键。");
        Assert(!(bool)input.Invoke(null, new object[] { new object() })!, "非输入对象不应被误判。");

        var editor = (FumenVisualEditorViewModel)RuntimeHelpers.GetUninitializedObject(type);
        var palette = new BulletPallete();
        var preview = new Bullet { ReferenceBulletPallete = palette };
        void Field(string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor, value);
        Field("quickPlacementPreviewObject", preview);
        Field("quickPlacementFactory", (Func<OngekiMovableObjectBase>)(() => new Bullet()));
        Field("quickPlacingDrag", true);
        var cancelled = 0;
        editor.QuickPlacementCancelled += () => cancelled++;
        editor.CancelQuickPlacement();
        Assert(!editor.IsQuickPlacementActive, "取消必须清除放置工厂。");
        Assert(preview.ReferenceBulletPallete == null, "取消必须解除预览与调色板的事件引用。");
        Assert(type.GetField("quickPlacementPreviewObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor) == null, "取消必须释放预览。");
        editor.CancelQuickPlacement();
        Assert(cancelled == 2, "重复取消必须安全并同步勾选状态。");

        Field("editorProjectData", new EditorProjectDataModel());
        var fumen = editor.Fumen;
        fumen.AddObject(palette);
        preview = new Bullet { ReferenceBulletPallete = palette };
        Field("quickPlacementPreviewObject", preview);
        Field("quickPlacementFactory", (Func<OngekiMovableObjectBase>)(() => new Bullet()));
        Field("quickPlacementFumen", fumen);
        var paletteChanged = type.GetMethod("OnQuickPlacementPalettesChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        fumen.BulletPalleteList.CollectionChanged += (System.Collections.Specialized.NotifyCollectionChangedEventHandler)
            paletteChanged.CreateDelegate(typeof(System.Collections.Specialized.NotifyCollectionChangedEventHandler), editor);
        fumen.RemoveObject(palette);
        Assert(!editor.IsQuickPlacementActive && preview.ReferenceBulletPallete == null,
            "删除或撤回新建调色板必须立即取消放置并解除预览引用。");
        Field("quickPlacementPreviewObject", new Bullet { ReferenceBulletPallete = palette });
        Field("quickPlacementFactory", (Func<OngekiMovableObjectBase>)(() => throw new InvalidOperationException("不得执行悬空工厂")));
        type.GetMethod("PlaceQuickObject", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, new object[] { new System.Windows.Point() });
        Assert(!editor.IsQuickPlacementActive, "放置时必须再次拒绝不存在的调色板。");
        Assert(HitRangeGeometry.BulletHalfWidth(BulletType.Circle) == 2, "圆弹判定半宽应为2。");
        Assert(HitRangeGeometry.BulletHalfWidth(BulletType.Needle) == 1 && HitRangeGeometry.BulletHalfWidth(BulletType.Square) == 1, "针弹和方弹判定半宽应为1。");
        var band = new List<PolygonVertex>();
        HitRangeGeometry.AddBand(band, new Vector2(0, -20), new Vector2(0, 20), 3, -5, 5, Vector4.One);
        Assert(band.Count == 6, "跨越可视区的激光段必须绘制裁剪后的矩形。");
        band.Clear();
        HitRangeGeometry.AddBand(band, new Vector2(0, 10), new Vector2(0, 20), 3, -5, 5, Vector4.One);
        Assert(band.Count == 0, "屏幕外激光段不应生成几何。");
        var path = new List<LineVertex>
        {
            new(new(0, 0), Vector4.One, VertexDash.Solider),
            new(new(0, 1), Vector4.Zero, VertexDash.Solider),
            new(new(0, 2), Vector4.One, VertexDash.Solider)
        };
        HitRangeGeometry.AddVisibleBands(band, path, 3, -5, 5, Vector4.One);
        Assert(band.Count == 0, "透明顶点必须保持断段。");
        path[1] = new(new(0, 1), Vector4.One, new VertexDash(0, 0));
        HitRangeGeometry.AddVisibleBands(band, path, 3, -5, 5, Vector4.One);
        Assert(band.Count == 0, "无效路径标记必须保持断段。");
        Console.WriteLine("子弹放置无窗口回归通过。");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
