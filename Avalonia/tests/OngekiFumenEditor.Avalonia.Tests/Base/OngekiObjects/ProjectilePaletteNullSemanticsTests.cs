using System.Text;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki;
using OngekiFumenEditor.Avalonia.Parser.Ogkr;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Ogkr;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.OngekiObjects;

public sealed class ProjectilePaletteNullSemanticsTests
{

    public ProjectilePaletteNullSemanticsTests()
    {
        // standardize palette generation logs through the IoC-backed Log;
        // keep these pure unit tests independent of headless DI setup
        Log.Initialize(new Log([]));
    }

    private static BulletPallete CreatePalette() => new BulletPallete()
    {
        StrID = "001",
        EditorName = "TestPallete",
        Speed = 2.5f,
        PlaceOffset = 3,
        RandomOffsetRange = 4,
        TypeValue = BulletType.Needle,
        TargetValue = Target.Player,
        ShooterValue = Shooter.Enemy,
        SizeValue = BulletSize.Large,
    };

    private static async Task<string> SerializeNyagekiAsync(OngekiFumen fumen)
        => Encoding.UTF8.GetString(await new DefaultNyagekiFumenFormatter().SerializeAsync(fumen));

    private static async Task<string> SerializeOgkrAsync(OngekiFumen fumen)
        => Encoding.UTF8.GetString(await new DefaultOngekiFumenFormatter().SerializeAsync(fumen));

    private static string[] SplitLines(string text)
        => text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

    private static int CountOccurrences(string text, string token)
        => text.Split(token).Length - 1;

    [Fact]
    public void NewBullet_HasNullPallete_AndLocalDefaults()
    {
        var bullet = new Bullet();

        Assert.Null(bullet.ReferenceBulletPallete);
        Assert.Equal(1f, bullet.Speed);
        Assert.Equal(0, bullet.RandomOffsetRange);
        Assert.Equal(0, bullet.PlaceOffset);
        Assert.Equal(BulletType.Circle, bullet.TypeValue);
        Assert.Equal(Target.FixField, bullet.TargetValue);
        Assert.Equal(Shooter.TargetHead, bullet.ShooterValue);
        Assert.Equal(BulletSize.Normal, bullet.SizeValue);
    }

    [Fact]
    public void NewBell_HasNullPallete_AndLocalDefaults()
    {
        var bell = new Bell();

        Assert.Null(bell.ReferenceBulletPallete);
        Assert.Equal(1f, bell.Speed);
        Assert.Equal(0, bell.RandomOffsetRange);
        Assert.Equal(0, bell.PlaceOffset);
        Assert.Equal(Target.FixField, bell.TargetValue);
        Assert.Equal(Shooter.TargetHead, bell.ShooterValue);
        Assert.Equal(BulletSize.Normal, bell.SizeValue);
        Assert.Equal(BulletType.Circle, bell.TypeValue);
        Assert.True(bell.IsOngekiDefaultBell());
    }

    [Fact]
    public void Bullet_SettingLocalValues_KeepsPalleteNull()
    {
        var bullet = new Bullet
        {
            Speed = 1.5f,
            PlaceOffset = 2,
            RandomOffsetRange = 3,
            TypeValue = BulletType.Needle,
            TargetValue = Target.Player,
            ShooterValue = Shooter.Enemy,
            SizeValue = BulletSize.Large,
        };

        Assert.Null(bullet.ReferenceBulletPallete);
        Assert.Equal(1.5f, bullet.Speed);
        Assert.Equal(2, bullet.PlaceOffset);
        Assert.Equal(3, bullet.RandomOffsetRange);
        Assert.Equal(BulletType.Needle, bullet.TypeValue);
        Assert.Equal(Target.Player, bullet.TargetValue);
        Assert.Equal(Shooter.Enemy, bullet.ShooterValue);
        Assert.Equal(BulletSize.Large, bullet.SizeValue);
    }

    [Fact]
    public void Bell_SettingEachLocalValue_KeepsPalleteNull()
    {
        var bell = new Bell();
        Assert.Null(bell.ReferenceBulletPallete);

        bell.Speed = 1.5f;
        Assert.Null(bell.ReferenceBulletPallete);

        bell.PlaceOffset = 2;
        Assert.Null(bell.ReferenceBulletPallete);

        bell.RandomOffsetRange = 3;
        Assert.Null(bell.ReferenceBulletPallete);

        bell.SizeValue = BulletSize.Large;
        Assert.Null(bell.ReferenceBulletPallete);

        bell.ShooterValue = Shooter.Enemy;
        Assert.Null(bell.ReferenceBulletPallete);

        bell.TargetValue = Target.Player;
        Assert.Null(bell.ReferenceBulletPallete);

        Assert.Equal(1.5f, bell.Speed);
        Assert.Equal(2, bell.PlaceOffset);
        Assert.Equal(3, bell.RandomOffsetRange);
        Assert.Equal(BulletSize.Large, bell.SizeValue);
        Assert.Equal(Shooter.Enemy, bell.ShooterValue);
        Assert.Equal(Target.Player, bell.TargetValue);
    }

    [Fact]
    public void Bullet_WithPallete_DelegatesGetters_AndLocalFieldUnaffected()
    {
        var bullet = new Bullet { Speed = 1.5f, PlaceOffset = 2 };

        bullet.ReferenceBulletPallete = CreatePalette();

        Assert.Equal(2.5f, bullet.Speed);
        Assert.Equal(3, bullet.PlaceOffset);
        Assert.Equal(4, bullet.RandomOffsetRange);
        Assert.Equal(BulletType.Needle, bullet.TypeValue);
        Assert.Equal(Target.Player, bullet.TargetValue);
        Assert.Equal(Shooter.Enemy, bullet.ShooterValue);
        Assert.Equal(BulletSize.Large, bullet.SizeValue);

        bullet.ReferenceBulletPallete = null;
        Assert.Equal(1.5f, bullet.Speed);
        Assert.Equal(2, bullet.PlaceOffset);
    }

    [Fact]
    public void Bell_WithPallete_DelegatesGetters_AndIsNotDefaultBell()
    {
        var bell = new Bell();
        Assert.True(bell.IsOngekiDefaultBell());

        bell.ReferenceBulletPallete = CreatePalette();

        Assert.Equal(2.5f, bell.Speed);
        Assert.Equal(3, bell.PlaceOffset);
        Assert.Equal(4, bell.RandomOffsetRange);
        Assert.Equal(Target.Player, bell.TargetValue);
        Assert.Equal(Shooter.Enemy, bell.ShooterValue);
        Assert.Equal(BulletSize.Large, bell.SizeValue);
        Assert.False(bell.IsOngekiDefaultBell());
    }

    [Fact]
    public void Bell_IsOngekiDefaultBell_FlipsFalseOnAnyLocalValueChange()
    {
        Assert.False(new Bell { Speed = 2 }.IsOngekiDefaultBell());
        Assert.False(new Bell { PlaceOffset = 1 }.IsOngekiDefaultBell());
        Assert.False(new Bell { RandomOffsetRange = 1 }.IsOngekiDefaultBell());
        Assert.False(new Bell { ShooterValue = Shooter.Enemy }.IsOngekiDefaultBell());
        Assert.False(new Bell { SizeValue = BulletSize.Large }.IsOngekiDefaultBell());
        Assert.False(new Bell { TargetValue = Target.Player }.IsOngekiDefaultBell());
    }

    [Fact]
    public void Bullet_Copy_NullPalleteSource_CopiesLocalValuesNotReference()
    {
        var from = new Bullet { Speed = 1.5f, PlaceOffset = 5, BulletDamageTypeValue = BulletDamageType.Danger };
        var to = new Bullet();

        to.Copy(from);

        Assert.Null(to.ReferenceBulletPallete);
        Assert.Equal(1.5f, to.Speed);
        Assert.Equal(5, to.PlaceOffset);
        Assert.Equal(BulletDamageType.Danger, to.BulletDamageTypeValue);
    }

    [Fact]
    public void Bullet_Copy_PalletedSource_CopiesPalleteReference()
    {
        var pallete = CreatePalette();
        var from = new Bullet { ReferenceBulletPallete = pallete };
        var to = new Bullet();

        to.Copy(from);

        Assert.Same(pallete, to.ReferenceBulletPallete);
        Assert.Equal(2.5f, to.Speed);
    }

    [Fact]
    public void Bell_Copy_NullPalleteSource_CopiesLocalValuesNotReference()
    {
        var from = new Bell { Speed = 1.5f, PlaceOffset = 5, SizeValue = BulletSize.Large };
        var to = new Bell();

        to.Copy(from);

        Assert.Null(to.ReferenceBulletPallete);
        Assert.Equal(1.5f, to.Speed);
        Assert.Equal(5, to.PlaceOffset);
        Assert.Equal(BulletSize.Large, to.SizeValue);
    }

    [Fact]
    public void Bell_Copy_PalletedSource_CopiesPalleteReference()
    {
        var pallete = CreatePalette();
        var from = new Bell { ReferenceBulletPallete = pallete };
        var to = new Bell();

        to.Copy(from);

        Assert.Same(pallete, to.ReferenceBulletPallete);
        Assert.Equal(2.5f, to.Speed);
    }

    [Fact]
    public async Task NyagekiWriter_NullPalleteBullet_WritesCustomBullet_WithSingleSpeedField()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new Bullet { Speed = 1.5f });

        var text = await SerializeNyagekiAsync(fumen);

        Assert.Contains("CustomBullet", text);
        Assert.Equal(1, CountOccurrences(text, ", Speed["));
        Assert.Equal(1, CountOccurrences(text, "CustomBullet"));
    }

    [Fact]
    public async Task NyagekiWriter_NullPalleteBell_WritesCustomBell()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new Bell { Speed = 1.5f });

        var text = await SerializeNyagekiAsync(fumen);

        Assert.Contains("CustomBell", text);
    }

    [Fact]
    public async Task NyagekiWriter_PalletedBell_WritesBellWithStrId()
    {
        var pallete = CreatePalette();
        var fumen = new OngekiFumen();
        fumen.AddObject(pallete);
        fumen.AddObject(new Bell { ReferenceBulletPallete = pallete });

        var text = await SerializeNyagekiAsync(fumen);

        Assert.Contains($"Bell\t:\t{pallete.StrID}\t:", text);
    }

    [Fact]
    public async Task OgkrWriter_NullPalleteBell_WritesCustomBellCommand_InsteadOfDefaultStrId()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new Bell { Speed = 1.5f });

        var text = await SerializeOgkrAsync(fumen);

        Assert.Contains("[CUSTOM_BEL]", text);
        Assert.DoesNotContain(SplitLines(text), line => line.StartsWith("BEL\t", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OgkrWriter_SkipsStandardizedDefaultBellBulletPaletteRow()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new StandardizedDefaultBellBulletPalette());

        var text = await SerializeOgkrAsync(fumen);

        Assert.Contains("[B_PALETTE]", text);
        Assert.DoesNotContain("BPL\t--\t", text);
    }

    [Fact]
    public async Task Standardize_DefaultBell_AttachesMarkerPalette_AndOgkrOmitsItsPalleteRow()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new Bell());

        StandardizeFormat.ConvertCustomProjectilesToPalleted(fumen);

        var bell = Assert.Single(fumen.Bells);
        Assert.IsType<StandardizedDefaultBellBulletPalette>(bell.ReferenceBulletPallete);

        var text = await SerializeOgkrAsync(fumen);
        Assert.DoesNotContain("BPL\t--\t", text);
        Assert.Contains(SplitLines(text), line => line.StartsWith("BEL\t", StringComparison.Ordinal) && line.EndsWith("\t--", StringComparison.Ordinal));
    }

    [Fact]
    public void Standardize_NonDefaultNullProjectiles_GenerateRealPalettesIntoList()
    {
        var fumen = new OngekiFumen();
        var bell = new Bell { Speed = 2 };
        var bullet = new Bullet { Speed = 3 };
        fumen.AddObject(bell);
        fumen.AddObject(bullet);

        StandardizeFormat.ConvertCustomProjectilesToPalleted(fumen);

        Assert.NotNull(bell.ReferenceBulletPallete);
        Assert.IsNotType<StandardizedDefaultBellBulletPalette>(bell.ReferenceBulletPallete);
        Assert.NotNull(bullet.ReferenceBulletPallete);
        Assert.NotSame(bell.ReferenceBulletPallete, bullet.ReferenceBulletPallete);
        Assert.Contains(fumen.BulletPalleteList, p => ReferenceEquals(p, bell.ReferenceBulletPallete));
        Assert.Contains(fumen.BulletPalleteList, p => ReferenceEquals(p, bullet.ReferenceBulletPallete));
        Assert.Equal(2, fumen.BulletPalleteList.Count());
    }
}
