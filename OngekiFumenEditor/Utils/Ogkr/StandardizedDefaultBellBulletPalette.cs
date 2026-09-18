using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;

namespace OngekiFumenEditor.Utils.Ogkr;

/// <summary>
/// Helper class to represent Ongeki's "--" default bell palette.
/// DO NOT USE outside ogkr standardization.
/// </summary>
public class StandardizedDefaultBellBulletPalette : BulletPallete
{
    public StandardizedDefaultBellBulletPalette()
    {
        StrID = Bell.OngekiDefaultBellPaletteName;
        // parameters don't matter; this class is just a marker so that ogkr can avoid outputting this
    }
}
