using Caliburn.Micro;
using System;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Properties;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    public sealed class SoflanGroupTreeNodeViewModel : PropertyChangedBase
    {
        private readonly FumenSoflanGroupListViewerViewModel owner;

        public SoflanGroupTreeNodeViewModel(FumenSoflanGroupListViewerViewModel owner, SoflanGroupTreeNode model)
        {
            this.owner = owner;
            Model = model;
            ColorBrush = BuildColorBrush(model);
        }

        public SoflanGroupTreeNode Model { get; }
        public SoflanGroupCategoryNode Category => Model as SoflanGroupCategoryNode;
        public SoflanGroupNode Group => Model as SoflanGroupNode;
        public SoflanGroupTreeNodeViewModel Parent { get; internal set; }
        public ObservableCollection<SoflanGroupTreeNodeViewModel> Children { get; } = new();

        public bool IsCategory => Model.IsCategory;
        public bool IsGroup => Model.IsGroup;
        public int Depth
        {
            get
            {
                var depth = 0;
                for (var current = Parent; current is not null; current = current.Parent)
                    depth++;
                return depth;
            }
        }
        public int? GroupId => Group?.Id;
        public bool IsDefaultGroup => Group?.IsDefault == true;
        public string IdLabel => Group is null ? string.Empty : $"#{Group.Id}";
        public bool HasChildren => Children.Count > 0;

        public string DisplayName => Group is not null
            ? (Group.IsDefault && string.IsNullOrWhiteSpace(Group.Name)
                ? Resources.SoflanGroupDefaultGroupName
                : string.IsNullOrWhiteSpace(Group.Name) ? $"#{Group.Id}" : Group.Name)
            : string.IsNullOrWhiteSpace(Model.Name) ? Resources.SoflanGroupCategoryName : Model.Name;

        public string DisplayNameWithId
        {
            get
            {
                if (GroupId is not int id)
                    return DisplayName;
                var suffix = $"#{id}";
                return DisplayName.EndsWith(suffix, StringComparison.Ordinal) ? DisplayName : $"{DisplayName} {suffix}";
            }
        }

        public Brush ColorBrush { get; }

        public string ToolTipText => IsDefaultGroup ? Resources.SoflanGroupDefaultGroupTipText : DisplayName;
        public string DesignToolTip => VisibleInDesign
            ? Resources.SoflanGroupDesignCheckedTipText
            : Resources.SoflanGroupDesignUncheckedTipText;
        public string PreviewToolTip => VisibleInPreview
            ? Resources.SoflanGroupPreviewCheckedTipText
            : Resources.SoflanGroupPreviewUncheckedTipText;
        public string DrawToolTip => IsDrawn
            ? Resources.SoflanGroupDrawCheckedTipText
            : Resources.SoflanGroupDrawUncheckedTipText;

        private bool isExpanded = true;
        public bool IsExpanded
        {
            get => isExpanded;
            set => Set(ref isExpanded, value);
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => Set(ref isSelected, value);
        }

        private bool visibleInDesign = true;
        public bool VisibleInDesign
        {
            get => visibleInDesign;
            set
            {
                if (visibleInDesign == value)
                    return;

                SetOwnVisibleInDesign(value);
                if (IsCategory)
                {
                    foreach (var child in Children)
                        child.CascadeVisibleInDesign(value);
                }
                else
                {
                    owner.SetGroupVisible(this);
                }

                Parent?.RefreshParentVisibility();
            }
        }

        private bool visibleInPreview = true;
        public bool VisibleInPreview
        {
            get => visibleInPreview;
            set
            {
                if (visibleInPreview == value)
                    return;

                SetOwnVisibleInPreview(value);
                if (IsCategory)
                {
                    foreach (var child in Children)
                        child.CascadeVisibleInPreview(value);
                }
                else
                {
                    owner.SetGroupVisible(this);
                }

                Parent?.RefreshParentVisibility();
            }
        }

        private bool isDrawn;
        public bool IsDrawn
        {
            get => isDrawn;
            set
            {
                if (isDrawn == value)
                    return;

                if (value)
                {
                    owner.SetDrawnGroup(this);
                }
                else
                {
                    SetOwnDrawn(false);
                    owner.ClearDrawnGroup(this);
                }
            }
        }

        internal IEnumerable<SoflanGroupTreeNodeViewModel> EnumerateGroupNodes()
        {
            if (IsGroup)
            {
                yield return this;
                yield break;
            }

            foreach (var child in Children)
                foreach (var node in child.EnumerateGroupNodes())
                    yield return node;
        }

        internal void SetDrawnSilent(bool value)
        {
            if (isDrawn == value)
                return;
            isDrawn = value;
            NotifyOfPropertyChange(() => IsDrawn);
            NotifyOfPropertyChange(() => DrawToolTip);
        }

        internal void ApplyDisplayState(bool design, bool preview, bool draw)
        {
            SetOwnVisibleInDesign(design);
            SetOwnVisibleInPreview(preview);
            SetOwnDrawn(draw);
        }

        internal void RefreshVisibilityFromChildren() => UpdateVisibilityFromChildren();

        internal void CascadeVisibleInDesign(bool value)
        {
            SetOwnVisibleInDesign(value);
            if (IsCategory)
            {
                foreach (var child in Children)
                    child.CascadeVisibleInDesign(value);
            }
            else
            {
                owner.SetGroupVisible(this);
            }
        }

        internal void CascadeVisibleInPreview(bool value)
        {
            SetOwnVisibleInPreview(value);
            if (IsCategory)
            {
                foreach (var child in Children)
                    child.CascadeVisibleInPreview(value);
            }
            else
            {
                owner.SetGroupVisible(this);
            }
        }

        internal void RefreshParentVisibility()
        {
            if (Parent is null)
                return;

            Parent.UpdateVisibilityFromChildren();
            Parent.RefreshParentVisibility();
        }

        private void UpdateVisibilityFromChildren()
        {
            if (!IsCategory || Children.Count == 0)
                return;

            SetOwnVisibleInDesign(Children.All(x => x.VisibleInDesign));
            SetOwnVisibleInPreview(Children.All(x => x.VisibleInPreview));
        }

        private void SetOwnVisibleInDesign(bool value)
        {
            if (visibleInDesign == value)
                return;
            visibleInDesign = value;
            NotifyOfPropertyChange(() => VisibleInDesign);
            NotifyOfPropertyChange(() => DesignToolTip);
        }

        private void SetOwnVisibleInPreview(bool value)
        {
            if (visibleInPreview == value)
                return;
            visibleInPreview = value;
            NotifyOfPropertyChange(() => VisibleInPreview);
            NotifyOfPropertyChange(() => PreviewToolTip);
        }

        private void SetOwnDrawn(bool value)
        {
            if (isDrawn == value)
                return;
            isDrawn = value;
            NotifyOfPropertyChange(() => IsDrawn);
            NotifyOfPropertyChange(() => DrawToolTip);
        }

        private static Brush BuildColorBrush(SoflanGroupTreeNode model)
        {
            if (model is not SoflanGroupNode group)
                return Brushes.Transparent;

            var color = SoflanGroupColor.Get(group.Id);
            var brush = new SolidColorBrush(ToColor(color));
            brush.Freeze();
            return brush;
        }

        private static Color ToColor(Vector4 color)
            => Color.FromArgb(
                (byte)(color.W * 255),
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255));
    }
}
