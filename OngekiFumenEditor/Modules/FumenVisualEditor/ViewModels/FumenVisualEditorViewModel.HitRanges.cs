namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private bool showBulletHitRanges = true;
        private bool showBeamHitRanges = true;
        public bool ShowBulletHitRanges
        {
            get => showBulletHitRanges;
            set => Set(ref showBulletHitRanges, value);
        }
        public bool ShowBeamHitRanges
        {
            get => showBeamHitRanges;
            set => Set(ref showBeamHitRanges, value);
        }
    }
}
