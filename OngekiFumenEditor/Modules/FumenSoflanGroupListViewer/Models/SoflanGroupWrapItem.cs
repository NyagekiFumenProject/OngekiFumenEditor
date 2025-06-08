using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
{
    public class SoflanGroupWrapItem : SoflanGroupDisplayItemListViewBase
    {
        public int SoflanGroupId { get; }

        public SoflanGroupWrapItem(int soflanGroupId)
        {
            SoflanGroupId = soflanGroupId;
        }

        public override string DisplayName
        {
            get => $"SoflanGroup #{SoflanGroupId}";
            set { } //ignore
        }

        private bool isSelected = true;
        public override bool IsSelected
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

        public override string ToString()
        {
            return $"SoflanGroupId:{SoflanGroupId}, IsSelected:{IsSelected}, IsDisplayInDesignMode:{IsDisplayInPreviewMode}, IsDisplayInDesignMode:{IsDisplayInPreviewMode}";
        }
    }
}
