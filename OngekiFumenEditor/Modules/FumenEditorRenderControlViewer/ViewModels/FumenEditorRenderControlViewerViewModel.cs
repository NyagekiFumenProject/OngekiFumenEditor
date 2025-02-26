using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
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

        public override PaneLocation PreferredLocation => PaneLocation.Right;

        public ObservableCollection<ControlItem> ControlItems { get; } = new ObservableCollection<ControlItem>();

        public FumenEditorRenderControlViewerViewModel()
        {
            DisplayName = Resources.FumenEditorRenderControlViewer;
            RebuildItems(false);
        }

        private async void RebuildItems(bool sortByDefault)
        {
            ControlItems.Clear();

            await IoC.Get<IDrawingManager>().WaitForGraphicsInitializationDone();
            var targets = IoC.GetAll<IFumenEditorDrawingTarget>().OrderBy(x => sortByDefault ? x.DefaultRenderOrder : x.CurrentRenderOrder).ToArray();
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
            // This shows how to customize the behavior of a drop.
            // Here we perform a swap, instead of just moving the dropped item.

            int higherIdx = Math.Max(e.OldIndex, e.NewIndex);
            int lowerIdx = Math.Min(e.OldIndex, e.NewIndex);

            if (lowerIdx < 0)
            {
                // The item came from the lower ListView
                // so just insert it.
                e.ItemsSource.Insert(higherIdx, e.DataItem);
            }
            else
            {
                // null values will cause an error when calling Move.
                // It looks like a bug in ObservableCollection to me.
                if (e.ItemsSource[lowerIdx] == null ||
                    e.ItemsSource[higherIdx] == null)
                    return;

                // The item came from the ListView into which
                // it was dropped, so swap it with the item
                // at the target index.
                e.ItemsSource.Move(lowerIdx, higherIdx);
                e.ItemsSource.Move(higherIdx - 1, lowerIdx);
            }

            UpdateRenderOrder();

            // Set this to 'Move' so that the OnListViewDrop knows to 
            // remove the item from the other ListView.
            e.Effects = DragDropEffects.Move;
        }
    }
}
