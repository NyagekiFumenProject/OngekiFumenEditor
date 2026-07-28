namespace OngekiFumenEditor.Avalonia.Base.EditorObjects
{
    public class SoflanGroupWrapItem : SoflanGroupDisplayItemListViewBase
    {
        public int SoflanGroupId { get; }

        public SoflanGroupWrapItem(int soflanGroupId)
        {
            SoflanGroupId = soflanGroupId;
            displayName = $"SoflanGroup #{SoflanGroupId}";

            if (soflanGroupId == 0)
                displayName = "Default " + displayName;
        }

        private string displayName;
        public override string DisplayName
        {
            get => displayName;
            set { } //ignore
        }

        private bool isSelected = false;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        private bool isDisplayInDesignMode = true;
        public override bool IsDisplayInDesignMode
        {
            get => isDisplayInDesignMode;
            set => SetProperty(ref isDisplayInDesignMode, value);
        }

        private bool isDisplayInPreviewMode = true;
        public override bool IsDisplayInPreviewMode
        {
            get => isDisplayInPreviewMode;
            set => SetProperty(ref isDisplayInPreviewMode, value);
        }

        private bool isDisplaySoflanDesignMode = false;
        public bool IsDisplaySoflanDesignMode
        {
            get => isDisplaySoflanDesignMode;
            set => SetProperty(ref isDisplaySoflanDesignMode, value);
        }

        public override string ToString()
        {
            return $"SoflanGroupId:{SoflanGroupId}, IsSelected:{IsSelected}, IsDisplayInDesignMode:{IsDisplayInPreviewMode}, IsDisplayInDesignMode:{IsDisplayInPreviewMode}, IsDisplaySoflanDesignMode:{IsDisplaySoflanDesignMode}";
        }
    }
}

