using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors;

public class DrawPlayerLocationHelper
{
    private readonly (Vector2 size, Vector2 position, float rotation)[] arr = { default };
    private readonly Texture texture;
    private readonly ITextureDrawing textureDrawing;
    private bool enableShowPlayerLocation;

    public DrawPlayerLocationHelper()
    {
        textureDrawing = IoC.Get<ITextureDrawing>();
        arr[0].size = new Vector2(48, 48);
        arr[0].rotation = 0f;

        texture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\playerLoc.png");

        UpdateProps();
        Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
    }

    private void UpdateProps()
    {
        enableShowPlayerLocation = Properties.EditorGlobalSetting.Default.EnableShowPlayerLocation;
    }

    private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Properties.EditorGlobalSetting.EnableShowPlayerLocation):
                UpdateProps();
                break;
            default:
                break;
        }
    }

    public void Draw(IFumenEditorDrawingContext target)
    {
        if (target.Editor.IsDesignMode)
            return;
        if (!enableShowPlayerLocation)
            return;

        var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentPlayTime, target.Editor);
        var apfLane = target.Editor.Fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid).OfType<AutoplayFaderLaneStart>()
            .LastOrDefault();
        var xGrid = apfLane?.CalulateXGrid(tGrid) ?? XGrid.Zero;

        var x = XGridCalculator.ConvertXGridToX(xGrid, target.Editor);
        var y = target.ConvertToY(tGrid);

        arr[0].position = new Vector2((float)x, (float)y);

        textureDrawing.Draw(target, texture, arr);
    }
}