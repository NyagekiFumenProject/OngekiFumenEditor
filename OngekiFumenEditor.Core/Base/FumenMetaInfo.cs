using Caliburn.Micro;
using OngekiFumenEditor.Core.Utils;
using System;
using System.ComponentModel;

namespace OngekiFumenEditor.Core.Base
{
    public class FumenMetaInfo : PropertyChangedBase
    {
        public class BpmDef : PropertyChangedBase
        {
            private double first = 240;
            public double First
            {
                get => first;
                set => Set(ref first, value);
            }

            public double Common { get; set; } = 240;
            public double Minimum { get; set; } = 240;
            public double Maximum { get; set; } = 240;
        }

        public class MetDef
        {
            public int Bunbo { get; set; } = 4;
            public int Bunshi { get; set; } = 4;
        }

        public FumenMetaInfo()
        {
            BpmDefinition = BpmDefinition;
        }

        public Version Version { get; set; } = new Version(1, 0, 0);
        public string Creator { get; set; } = "";

        private BpmDef bpmDefinition = new BpmDef();
        public BpmDef BpmDefinition
        {
            get => bpmDefinition;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(bpmDefinition, value, OnBpmDefinitionPropChanged);
                Set(ref bpmDefinition, value);
            }
        }

        private void OnBpmDefinitionPropChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(() => BpmDefinition);
        }

        public MetDef MeterDefinition { get; set; } = new MetDef();
        public int TRESOLUTION { get; set; } = 1920;
        public int XRESOLUTION { get; set; } = 4096;
        public int ClickDefinition { get; set; } = 1920;
        public bool Tutorial { get; set; } = false;
        public double BulletDamage { get; set; } = 1;
        public double HardBulletDamage { get; set; } = 2;
        public double DangerBulletDamage { get; set; } = 4;
        public double BeamDamage { get; set; } = 2;
        public float ProgJudgeBpm { get; set; } = 240;
    }
}

