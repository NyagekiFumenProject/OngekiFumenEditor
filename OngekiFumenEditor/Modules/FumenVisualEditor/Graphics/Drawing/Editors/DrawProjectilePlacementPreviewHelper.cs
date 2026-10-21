using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell;
using OngekiFumenEditor.Utils;
using System;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors;

public sealed class DrawProjectilePlacementPreviewHelper : IDisposable
{
    private readonly TextureInstance[] instances = new TextureInstance[1];
    private IFumenBulletPalleteListViewer palleteListViewer;
    private BulletDrawingTarget bulletDrawingTarget;
    private BellDrawingTarget bellDrawingTarget;

    public DrawProjectilePlacementPreviewHelper(IFumenBulletPalleteListViewer palleteListViewer,
        BulletDrawingTarget bulletDrawingTarget, BellDrawingTarget bellDrawingTarget)
    {
        this.palleteListViewer = palleteListViewer;
        this.bulletDrawingTarget = bulletDrawingTarget;
        this.bellDrawingTarget = bellDrawingTarget;
    }

    public void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
    {
        var editor = target.Editor;
        if (!editor.IsDesignMode || palleteListViewer is null || !palleteListViewer.IsRequestPlaceBatchProjectilePreview)
            return;

        var request = palleteListViewer.RequestBatchPlaceProjectileInfo;
        if (request is null || !ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo))
            return;

        var preview = editor.BatchProjectilePlacementPreview;
        IImage texture;
        Vector2 size;
        var originOffset = Vector2.Zero;
        if (request.IsBell && preview is Bell bell && ReferenceEquals(bell.ReferenceBulletPallete, request.Pallete))
            bellDrawingTarget.GetSprite(out texture, out size);
        else if (!request.IsBell && preview is Bullet bullet && ReferenceEquals(bullet.ReferenceBulletPallete, request.Pallete))
            bulletDrawingTarget.GetSprite(bullet, out texture, out size, out originOffset);
        else
            return;

        var x = XGridCalculator.ConvertXGridToX(preview.XGrid, editor);
        var y = target.ConvertToViewRelativeY_DefaultSoflanGroup(preview.TGrid);
        var position = new Vector2((float)x, (float)y) + originOffset;
        instances[0] = new TextureInstance(size, position, 0, new Vector4(1, 1, 1, 0.65f));
        builder.DrawTexture(texture, instances);
    }

    public void Dispose()
    {
        // The normal drawing targets own the shared textures, including backend rebuilds.
        palleteListViewer = null;
        bulletDrawingTarget = null;
        bellDrawingTarget = null;
    }
}
