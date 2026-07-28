using System;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.ViewModels;

public class OngekiFumenModelProxy : ObservableObject
{
    private readonly OngekiFumen fumen;

    public OngekiFumenModelProxy(OngekiFumen fumen)
    {
        this.fumen = fumen;
    }

    private FumenMetaInfo FumenMetaInfo => fumen.MetaInfo;

    public int VersionMajor
    {
        get => FumenMetaInfo?.Version.Major ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.Version = new Version(value, VersionMinor, VersionBuild);
            OnPropertyChanged();
        }
    }

    public int VersionMinor
    {
        get => FumenMetaInfo?.Version.Minor ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.Version = new Version(VersionMajor, value, VersionBuild);
            OnPropertyChanged();
        }
    }

    public int VersionBuild
    {
        get => FumenMetaInfo?.Version.Build ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.Version = new Version(VersionMajor, VersionMinor, value);
            OnPropertyChanged();
        }
    }

    public string Creator
    {
        get => FumenMetaInfo?.Creator ?? string.Empty;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.Creator = value;
            OnPropertyChanged();
        }
    }

    public double MinBpm
    {
        get => FumenMetaInfo?.BpmDefinition.Minimum ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.BpmDefinition.Minimum = value;
            OnPropertyChanged();
        }
    }

    public double MaxBpm
    {
        get => FumenMetaInfo?.BpmDefinition.Maximum ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.BpmDefinition.Maximum = value;
            OnPropertyChanged();
        }
    }

    public double CommonBpm
    {
        get => FumenMetaInfo?.BpmDefinition.Common ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.BpmDefinition.Common = value;
            OnPropertyChanged();
        }
    }

    public double FirstBpm
    {
        get => FumenMetaInfo?.BpmDefinition.First ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.BpmDefinition.First = value;
            OnPropertyChanged();
        }
    }

    public int Bunbo
    {
        get => FumenMetaInfo?.MeterDefinition.Bunbo ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.MeterDefinition.Bunbo = value;
            OnPropertyChanged();
        }
    }

    public int Bunshi
    {
        get => FumenMetaInfo?.MeterDefinition.Bunshi ?? 0;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.MeterDefinition.Bunshi = value;
            OnPropertyChanged();
        }
    }

    public int TRESOLUTION
    {
        get => FumenMetaInfo?.TRESOLUTION ?? 1920;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.TRESOLUTION = value;
            OnPropertyChanged();
        }
    }

    public int XRESOLUTION
    {
        get => FumenMetaInfo?.XRESOLUTION ?? 4096;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.XRESOLUTION = value;
            OnPropertyChanged();
        }
    }

    public int ClickDefinition
    {
        get => FumenMetaInfo?.ClickDefinition ?? 1920;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.ClickDefinition = value;
            OnPropertyChanged();
        }
    }

    public bool Tutorial
    {
        get => FumenMetaInfo?.Tutorial ?? false;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.Tutorial = value;
            OnPropertyChanged();
        }
    }

    public double BulletDamage
    {
        get => FumenMetaInfo?.BulletDamage ?? 1;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.BulletDamage = value;
            OnPropertyChanged();
        }
    }

    public double HardBulletDamage
    {
        get => FumenMetaInfo?.HardBulletDamage ?? 2;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.HardBulletDamage = value;
            OnPropertyChanged();
        }
    }

    public double DangerBulletDamage
    {
        get => FumenMetaInfo?.DangerBulletDamage ?? 4;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.DangerBulletDamage = value;
            OnPropertyChanged();
        }
    }

    public double BeamDamage
    {
        get => FumenMetaInfo?.BeamDamage ?? 2;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.BeamDamage = value;
            OnPropertyChanged();
        }
    }

    public float ProgJudgeBpm
    {
        get => FumenMetaInfo?.ProgJudgeBpm ?? 240;
        set
        {
            if (FumenMetaInfo is null)
                return;
            FumenMetaInfo.ProgJudgeBpm = value;
            OnPropertyChanged();
        }
    }
}

