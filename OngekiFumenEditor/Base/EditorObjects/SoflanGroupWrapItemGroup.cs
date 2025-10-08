using AngleSharp.Dom;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class SoflanGroupWrapItemGroup : SoflanGroupDisplayItemListViewBase
    {
        public SoflanGroupWrapItemGroup(SoflanGroupWrapItemGroup parent = default)
        {
            Parent = parent;
        }

        private int isUpdatingChildrenCount = 0;

        private bool isExpanded = true;
        public virtual bool IsExpanded
        {
            get => isExpanded;
            set
            {
                Set(ref isExpanded, value);
                NotifyItemSourceChanged();
            }
        }

        private bool isDisplayInDesignMode = true;
        public override bool IsDisplayInDesignMode
        {
            get => isDisplayInDesignMode;
            set
            {
                Set(ref isDisplayInDesignMode, value);
                isUpdatingChildrenCount++;
                foreach (var child in Children)
                    child.IsDisplayInDesignMode = IsDisplayInDesignMode;
                isUpdatingChildrenCount--;
            }
        }

        private bool isDisplayInPreviewMode = true;
        public override bool IsDisplayInPreviewMode
        {
            get => isDisplayInPreviewMode;
            set
            {
                Set(ref isDisplayInPreviewMode, value);
                isUpdatingChildrenCount++;
                foreach (var child in Children)
                    child.IsDisplayInPreviewMode = IsDisplayInPreviewMode;
                isUpdatingChildrenCount--;
            }
        }

        private ObservableCollection<SoflanGroupDisplayItemListViewBase> children { get; } = new();

        public IReadOnlyList<SoflanGroupDisplayItemListViewBase> Children => children;

        public IReadOnlyList<SoflanGroupDisplayItemListViewBase> cachedDisplayableItemSource;
        public IReadOnlyList<SoflanGroupDisplayItemListViewBase> DisplayableItemSource
        {
            get
            {
                if (cachedDisplayableItemSource != null)
                    return cachedDisplayableItemSource;

                var list = new List<SoflanGroupDisplayItemListViewBase>();

                if (IsExpanded)
                {
                    foreach (var item in children)
                    {
                        list.Add(item);
                        if (item is SoflanGroupWrapItemGroup childGroup)
                        {
                            list.AddRange(childGroup.DisplayableItemSource);
                        }
                    }
                }

                return cachedDisplayableItemSource = list;
            }
        }

        private string displayName;
        public override string DisplayName
        {
            get => displayName;
            set => Set(ref displayName, value);
        }

        private bool hasChildren = false;
        public bool HasChildren
        {
            get => hasChildren;
            set => Set(ref hasChildren, value);
        }

        public SoflanGroupWrapItemGroup()
        {
            children.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        foreach (var newItem in e.NewItems)
                        {
                            if (newItem is SoflanGroupDisplayItemListViewBase newChild)
                            {
                                newChild.Parent = this;
                                newChild.Level = Level + 1;

                                newChild.PropertyChanged += OnChildPropertyChangedHandler;
                            }
                        }

                        NotifyItemSourceChanged();
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    {
                        foreach (var oldItem in e.OldItems)
                        {
                            if (oldItem is SoflanGroupDisplayItemListViewBase oldChild)
                            {
                                oldChild.Parent = default;
                                oldChild.Level = default;

                                oldChild.PropertyChanged -= OnChildPropertyChangedHandler;
                            }
                        }

                        NotifyItemSourceChanged();
                    }
                    break;
                default:
                    break;
            }

            HasChildren = children.Count > 0;
        }

        protected void NotifyItemSourceChanged()
        {
            cachedDisplayableItemSource = default;
            OnPropertyChanged(new(nameof(DisplayableItemSource)));
            Parent?.NotifyItemSourceChanged();
        }

        public void Add(SoflanGroupDisplayItemListViewBase item)
        {
            if (item == null) return;
            if (!children.Contains(item))
                children.Add(item);
        }

        public void Remove(SoflanGroupDisplayItemListViewBase item)
        {
            if (item == null) return;
            if (children.Contains(item))
                children.Remove(item);
        }

        public override string ToString()
        {
            return $"Group: {DisplayName}, Count:{Children.Count}, TotalCount:{DisplayableItemSource.Count}, IsExpanded:{IsExpanded}";
        }

        private void OnChildPropertyChangedHandler(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SoflanGroupDisplayItemListViewBase.IsDisplayInPreviewMode):
                    if (isUpdatingChildrenCount == 0)
                    {
                        var beforeValue = IsDisplayInPreviewMode;
                        var newValue = Children.All(c => c.IsDisplayInPreviewMode);
                        if (beforeValue != newValue)
                        {
                            isDisplayInPreviewMode = newValue;
                            NotifyOfPropertyChange(() => IsDisplayInPreviewMode);
                        }
                    }
                    break;
                case nameof(SoflanGroupDisplayItemListViewBase.IsDisplayInDesignMode):
                    if (isUpdatingChildrenCount == 0)
                    {
                        var beforeValue = IsDisplayInDesignMode;
                        var newValue = Children.All(c => c.IsDisplayInDesignMode);
                        if (beforeValue != newValue)
                        {
                            isDisplayInDesignMode = newValue;
                            NotifyOfPropertyChange(() => IsDisplayInDesignMode);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public void InsertBefore(SoflanGroupDisplayItemListViewBase item, SoflanGroupWrapItem insertBefore)
        {
            var index = -1;
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] == insertBefore)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                throw new Exception($"Children not contains insertBefore");

            children.Insert(index, item);
        }
    }
}
