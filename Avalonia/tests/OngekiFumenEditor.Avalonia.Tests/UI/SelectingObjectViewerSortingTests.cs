using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class SelectingObjectViewerSortingTests
{
    [AvaloniaFact]
    public void ColumnsExposeExplicitSortMembers()
    {
        var view = new FumenEditorSelectingObjectViewerView();
        var grid = Assert.IsType<DataGrid>(view.FindControl<DataGrid>("listView"));

        Assert.True(grid.CanUserSortColumns);
        Assert.Equal(nameof(SelectedObjectRow.Name), grid.Columns[0].SortMemberPath);
        Assert.Equal(nameof(SelectedObjectRow.TGrid), grid.Columns[1].SortMemberPath);
        Assert.Equal(nameof(SelectedObjectRow.SoflanGroup), grid.Columns[2].SortMemberPath);
        Assert.False(grid.Columns[3].CanUserSort);
    }

    [AvaloniaFact]
    public void ColumnsToggleSortingAndKeepSortAfterSourceRefresh()
    {
        var fumen = CreateFumenWithSoflanArea();
        var firstTap = new Tap { TGrid = new TGrid(3), XGrid = new XGrid(0) };
        var bell = new Bell { TGrid = new TGrid(1), XGrid = new XGrid(5) };
        var secondTap = new Tap { TGrid = new TGrid(2), XGrid = new XGrid(0) };
        var bpm = new BPMChange { TGrid = new TGrid(4) };
        var rows = new[]
        {
            new SelectedObjectRow(firstTap, fumen),
            new SelectedObjectRow(bell, fumen),
            new SelectedObjectRow(secondTap, fumen),
            new SelectedObjectRow(bpm, fumen)
        };
        var source = new ObservableCollection<SelectedObjectRow>(rows);
        var collectionView = new DataGridCollectionView(source);

        AssertSort(collectionView, rows, nameof(SelectedObjectRow.Name), x => x.Name);
        Assert.Equal(
            new ISelectableObject[] { firstTap, secondTap },
            collectionView.Cast<SelectedObjectRow>()
                .Where(x => x.Object is Tap)
                .Select(x => x.Object));

        AssertSort(collectionView, rows, nameof(SelectedObjectRow.TGrid), x => x.TGrid);
        AssertSort(collectionView, rows, nameof(SelectedObjectRow.SoflanGroup), x => x.SoflanGroup);

        ApplySort(collectionView, nameof(SelectedObjectRow.Name), ListSortDirection.Ascending);
        var refreshedRows = rows.Reverse().ToArray();
        source.Clear();
        foreach (var row in refreshedRows)
            source.Add(row);

        Assert.Equal(
            refreshedRows.OrderBy(x => x.Name).Select(x => x.Object),
            collectionView.Cast<SelectedObjectRow>().Select(x => x.Object));
    }

    [AvaloniaFact]
    public void RefreshRecomputesSoflanGroupAndPreservesGridSelection()
    {
        var fumen = CreateFumenWithSoflanArea();
        var inside = new Tap
        {
            TGrid = new TGrid(1),
            XGrid = new XGrid(0),
            IsSelected = true
        };
        var outside = new Tap
        {
            TGrid = new TGrid(1),
            XGrid = new XGrid(5),
            IsSelected = true
        };
        fumen.AddObject(inside);
        fumen.AddObject(outside);

        var editor = new FumenVisualEditorViewModel { Fumen = fumen };
        try
        {
            var viewModel = CreateViewer(editor);
            var originalRow = Assert.Single(viewModel.EditorSelectObjects.Cast<SelectedObjectRow>(),
                x => ReferenceEquals(x.Object, inside));
            Assert.Equal(7, originalRow.SoflanGroup);
            Assert.Equal(0, Assert.Single(viewModel.EditorSelectObjects.Cast<SelectedObjectRow>(),
                x => ReferenceEquals(x.Object, outside)).SoflanGroup);
            viewModel.SelectedItems.Add(originalRow);

            inside.TGrid = new TGrid(2);
            viewModel.RefreshCommand.Execute(null);

            var refreshedRow = Assert.Single(viewModel.EditorSelectObjects.Cast<SelectedObjectRow>(),
                x => ReferenceEquals(x.Object, inside));
            Assert.NotSame(originalRow, refreshedRow);
            Assert.Equal(new TGrid(2), refreshedRow.TGrid);
            Assert.Same(refreshedRow, Assert.Single(viewModel.SelectedItems));
        }
        finally
        {
            editor.Setting.Dispose();
        }
    }

    [AvaloniaFact]
    public void LargeSelectionSortsWithinBudget()
    {
        const int rowCount = 5000;
        var rows = Enumerable.Range(0, rowCount)
            .Select(i => new SelectedObjectRow(new Tap
            {
                TGrid = new TGrid(rowCount - i),
                XGrid = new XGrid(i % 10)
            }))
            .ToArray();
        var collectionView = new DataGridCollectionView(rows);

        var stopwatch = Stopwatch.StartNew();
        ApplySort(collectionView, nameof(SelectedObjectRow.TGrid), ListSortDirection.Ascending);
        stopwatch.Stop();

        Assert.Equal(1, collectionView.Cast<SelectedObjectRow>().First().TGrid.Unit);
        Assert.Equal(rowCount, collectionView.Cast<SelectedObjectRow>().Last().TGrid.Unit);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Sorting {rowCount} selected objects took {stopwatch.Elapsed}.");
    }

    private static void AssertSort<TKey>(
        DataGridCollectionView collectionView,
        IReadOnlyCollection<SelectedObjectRow> rows,
        string propertyName,
        Func<SelectedObjectRow, TKey> keySelector)
    {
        ApplySort(collectionView, propertyName, ListSortDirection.Ascending);
        Assert.Equal(
            rows.OrderBy(keySelector).Select(x => x.Object),
            collectionView.Cast<SelectedObjectRow>().Select(x => x.Object));

        ApplySort(collectionView, propertyName, ListSortDirection.Descending);
        Assert.Equal(
            rows.OrderByDescending(keySelector).Select(x => x.Object),
            collectionView.Cast<SelectedObjectRow>().Select(x => x.Object));
    }

    private static void ApplySort(
        DataGridCollectionView collectionView,
        string propertyName,
        ListSortDirection direction)
    {
        collectionView.SortDescriptions.Clear();
        collectionView.SortDescriptions.Add(DataGridSortDescription.FromPath(propertyName, direction));
    }

    private static OngekiFumen CreateFumenWithSoflanArea()
    {
        var fumen = new OngekiFumen();
        var area = new IndividualSoflanArea
        {
            SoflanGroup = 7,
            TGrid = new TGrid(0),
            XGrid = new XGrid(-1)
        };
        area.EndIndicator.TGrid = new TGrid(10);
        area.EndIndicator.XGrid = new XGrid(1);
        fumen.AddObject(area);
        return fumen;
    }

    private static FumenEditorSelectingObjectViewerViewModel CreateViewer(FumenVisualEditorViewModel editor)
    {
        var injectedConstructor = typeof(FumenEditorSelectingObjectViewerViewModel)
            .GetConstructor([typeof(IEditorDocumentManager)]);
        var viewModel = injectedConstructor is null
            ? (FumenEditorSelectingObjectViewerViewModel)Activator.CreateInstance(
                typeof(FumenEditorSelectingObjectViewerViewModel))!
            : (FumenEditorSelectingObjectViewerViewModel)injectedConstructor.Invoke(
                [new StubEditorDocumentManager { Current = editor }]);
        viewModel.Editor = editor;
        return viewModel;
    }

    private sealed class StubEditorDocumentManager : IEditorDocumentManager
    {
        private FumenVisualEditorViewModel? current;

        public FumenVisualEditorViewModel Current
        {
            get => current!;
            set => current = value;
        }

        public FumenVisualEditorViewModel CurrentActivatedEditor => current!;

        public event IEditorDocumentManager.NotifyCreateFunc OnNotifyCreated
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.ActivateEditorChangedFunc OnActivateEditorChanged
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.NotifyDestoryFunc OnNotifyDestoryed
        {
            add { }
            remove { }
        }

        public IEnumerable<FumenVisualEditorViewModel> GetCurrentEditors() =>
            current is null ? [] : [current];

        public void NotifyActivate(FumenVisualEditorViewModel editor) => current = editor;

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                current = null;
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor) => current = editor;

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                current = null;
        }
    }
}
