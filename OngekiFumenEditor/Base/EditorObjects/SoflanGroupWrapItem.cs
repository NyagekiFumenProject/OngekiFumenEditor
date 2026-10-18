using OngekiFumenEditor.Properties;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class SoflanGroupWrapItem : SoflanGroupDisplayItemListViewBase
    {
        public int SoflanGroupId { get; }

        public SoflanGroupWrapItem(int soflanGroupId)
        {
            SoflanGroupId = soflanGroupId;
            displayName = SoflanGroupId.ToString();
            if (soflanGroupId == 0)
                displayName = Resources.SoflanGroupDefaultName;
        }

        private string displayName;
        public override string DisplayName
        {
            get => displayName;
            set { } //ignore
        }

        public string SelectionDisplayName => SoflanGroupId == 0 || string.IsNullOrWhiteSpace(Parent?.DisplayName) || Parent.DisplayName == "default" || Parent.DisplayName == "root" || Parent.DisplayName == SoflanGroupId.ToString() ? DisplayName : $"{Parent.DisplayName}（{SoflanGroupId}）";

        private bool isSelected = false;
        public bool IsSelected
        {
            get => isSelected;
            set => Set(ref isSelected, value);
        }

        private bool isDisplayInDesignMode = true;
        public override bool IsDisplayInDesignMode
        {
            get => isDisplayInDesignMode;
            set => Set(ref isDisplayInDesignMode, value);
        }

        private bool isDisplayInPreviewMode = true;
        public override bool IsDisplayInPreviewMode
        {
            get => isDisplayInPreviewMode;
            set => Set(ref isDisplayInPreviewMode, value);
        }

        private bool isDisplaySoflanDesignMode = false;
        public bool IsDisplaySoflanDesignMode
        {
            get => isDisplaySoflanDesignMode;
            set => Set(ref isDisplaySoflanDesignMode, value);
        }

        public override string ToString()
        {
            return $"SoflanGroupId:{SoflanGroupId}, IsSelected:{IsSelected}, IsDisplayInDesignMode:{IsDisplayInPreviewMode}, IsDisplayInDesignMode:{IsDisplayInPreviewMode}, IsDisplaySoflanDesignMode:{IsDisplaySoflanDesignMode}";
        }
    }
}
