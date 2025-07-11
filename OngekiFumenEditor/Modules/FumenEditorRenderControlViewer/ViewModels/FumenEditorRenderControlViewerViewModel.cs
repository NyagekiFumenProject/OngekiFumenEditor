﻿using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.CodeAnalysis.Differencing;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WPF.JoshSmith.ServiceProviders.UI;

namespace OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.ViewModels
{
    [Export(typeof(IFumenEditorRenderControlViewer))]
    public class FumenEditorRenderControlViewerViewModel : Tool, IFumenEditorRenderControlViewer
    {
        private ListViewDragDropManager<ControlItem> listDragManager;

        public class ControlItem : PropertyChangedBase
        {
            public ControlItem(IFumenEditorDrawingTarget target)
            {
                this.target = target;
                Name = target.GetType().Name.TrimEnd("DrawingTarget").TrimEnd("DrawTarget");
            }

            public string Name { get; init; }
            private readonly IFumenEditorDrawingTarget target;

            public IFumenEditorDrawingTarget Target => target;

            public bool IsDesignEnable
            {
                get { return target.Visible.HasFlag(DrawingVisible.Design); }
                set
                {
                    if (value)
                        target.Visible |= DrawingVisible.Design;
                    else
                        target.Visible &= ~DrawingVisible.Design;
                    NotifyOfPropertyChange(() => IsDesignEnable);
                }
            }

            public bool IsPreviewEnable
            {
                get { return target.Visible.HasFlag(DrawingVisible.Preview); }
                set
                {
                    if (value)
                        target.Visible |= DrawingVisible.Preview;
                    else
                        target.Visible &= ~DrawingVisible.Preview;
                    NotifyOfPropertyChange(() => IsPreviewEnable);
                }
            }

            public int RenderOrder
            {
                get { return target.CurrentRenderOrder; }
                set
                {
                    target.CurrentRenderOrder = value;
                    NotifyOfPropertyChange(() => RenderOrder);
                }
            }

            public override string ToString() => $"[{target.Visible}] : {Name}";
        }

        private FumenVisualEditorViewModel editor;
        public FumenVisualEditorViewModel Editor
        {
            get
            {
                return editor;
            }
            set
            {
                Set(ref editor, value);
                RebuildItems(false);
            }
        }

        public override PaneLocation PreferredLocation => PaneLocation.Right;

        public ObservableCollection<ControlItem> ControlItems { get; } = new ObservableCollection<ControlItem>();

        public FumenEditorRenderControlViewerViewModel()
        {
            DisplayName = Resources.FumenEditorRenderControlViewer;

            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
        }

        private async void RebuildItems(bool sortByDefault)
        {
            ControlItems.Clear();

            if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
                return;

            await editor.WaitForRenderInitializationIsDone();

            if (editor != Editor)
                return;

            if (editor.CurrentDrawingTargets == null)
            {
                Log.LogWarn($"retrieve editor({editor.DisplayName})'s drawing targets failed");
                return;
            }

            var targets = editor.CurrentDrawingTargets.OrderBy(x => sortByDefault ? x.DefaultRenderOrder : x.CurrentRenderOrder).ToArray();
            ControlItems.AddRange(targets.Select(x => new ControlItem(x)));
            UpdateRenderOrder();
        }

        private void UpdateRenderOrder()
        {
            for (int i = 0; i < ControlItems.Count; i++)
                ControlItems[i].RenderOrder = i;
        }

        public void ResetDefault()
        {
            RebuildItems(true);
            for (int i = 0; i < ControlItems.Count; i++)
            {
                ControlItems[i].Target.Visible = ControlItems[i].Target.DefaultVisible;
                ControlItems[i].Refresh();
            }
        }

        public void Save()
        {
            IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor?.SaveRenderOrderVisible();
        }

        public void OnListLoaded(FrameworkElement list)
        {
            listDragManager = new ListViewDragDropManager<ControlItem>(list as ListView);
            listDragManager.ShowDragAdorner = true;
            listDragManager.DragAdornerOpacity = 0.75f;

            listDragManager.ProcessDrop += ListDragManager_ProcessDrop;

            Log.LogDebug($"ListViewDragDropManager created.");
        }

        private void ListDragManager_ProcessDrop(object sender, ProcessDropEventArgs<ControlItem> e)
        {
            if (e.ItemsSource is not ObservableCollection<ControlItem> list)
                return;

            // This shows how to customize the behavior of a drop.
            // Here we perform a swap, instead of just moving the dropped item.

            int higherIdx = Math.Max(e.OldIndex, e.NewIndex);
            int lowerIdx = Math.Min(e.OldIndex, e.NewIndex);

            if (lowerIdx < 0)
            {
                // The item came from the lower ListView
                // so just insert it.
                list.Insert(higherIdx, e.DataItem);
            }
            else
            {
                // null values will cause an error when calling Move.
                // It looks like a bug in ObservableCollection to me.
                if (list[lowerIdx] == null ||
                    list[higherIdx] == null)
                    return;

                // The item came from the ListView into which
                // it was dropped, so swap it with the item
                // at the target index.
                list.Move(lowerIdx, higherIdx);
                list.Move(higherIdx - 1, lowerIdx);
            }

            UpdateRenderOrder();

            // Set this to 'Move' so that the OnListViewDrop knows to 
            // remove the item from the other ListView.
            e.Effects = DragDropEffects.Move;
        }
    }
}
