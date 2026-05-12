using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using F1Telemetry.Core.Abstractions;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Exposes a bindable paged projection over an in-memory item list.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class PagedCollectionViewModel<T> : ViewModelBase
{
    private const int DefaultPageSize = 6;
    private const int DefaultMinPageSize = 1;
    private const int DefaultMaxPageSize = 50;

    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;
    private IReadOnlyList<T> _sourceItems = Array.Empty<T>();
    private int _pageIndex;
    private int _pageSize = DefaultPageSize;

    /// <summary>
    /// Initializes a paged collection view model.
    /// </summary>
    public PagedCollectionViewModel()
    {
        Items = new ObservableCollection<T>();
        _previousPageCommand = new RelayCommand(MovePrevious, () => CanMovePrevious);
        _nextPageCommand = new RelayCommand(MoveNext, () => CanMoveNext);
    }

    /// <summary>
    /// Gets the current page items.
    /// </summary>
    public ObservableCollection<T> Items { get; }

    /// <summary>
    /// Gets the zero-based current page index.
    /// </summary>
    public int PageIndex
    {
        get => _pageIndex;
        private set
        {
            if (SetProperty(ref _pageIndex, value))
            {
                RaisePagingPropertiesChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current page size.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        private set
        {
            if (SetProperty(ref _pageSize, Math.Max(DefaultMinPageSize, value)))
            {
                RaisePagingPropertiesChanged();
            }
        }
    }

    /// <summary>
    /// Gets the total item count.
    /// </summary>
    public int TotalItemCount => _sourceItems.Count;

    /// <summary>
    /// Gets the total page count.
    /// </summary>
    public int PageCount => TotalItemCount == 0 ? 0 : (int)Math.Ceiling(TotalItemCount / (double)PageSize);

    /// <summary>
    /// Gets a value indicating whether the current page can move backward.
    /// </summary>
    public bool CanMovePrevious => PageIndex > 0;

    /// <summary>
    /// Gets a value indicating whether the current page can move forward.
    /// </summary>
    public bool CanMoveNext => PageIndex + 1 < PageCount;

    /// <summary>
    /// Gets a value indicating whether pagination controls should be shown.
    /// </summary>
    public bool HasMultiplePages => PageCount > 1;

    /// <summary>
    /// Gets the human-readable page status text.
    /// </summary>
    public string PageText => PageCount == 0
        ? "第 0 / 0 页"
        : string.Format(CultureInfo.InvariantCulture, "第 {0} / {1} 页", PageIndex + 1, PageCount);

    /// <summary>
    /// Gets the command that moves to the previous page.
    /// </summary>
    public ICommand PreviousPageCommand => _previousPageCommand;

    /// <summary>
    /// Gets the command that moves to the next page.
    /// </summary>
    public ICommand NextPageCommand => _nextPageCommand;

    /// <summary>
    /// Replaces the source items and refreshes the current page.
    /// </summary>
    /// <param name="items">The new source items.</param>
    /// <param name="resetPage">Whether to reset to the first page.</param>
    public void SetItems(IEnumerable<T> items, bool resetPage = true)
    {
        _sourceItems = (items ?? Array.Empty<T>()).ToArray();
        if (resetPage)
        {
            _pageIndex = 0;
        }

        ClampPageIndex();
        RefreshCurrentPage();
        RaisePagingPropertiesChanged();
    }

    /// <summary>
    /// Sets the page size from the available viewport height and estimated item height.
    /// </summary>
    /// <param name="viewportHeight">The available viewport height.</param>
    /// <param name="estimatedItemHeight">The estimated item height.</param>
    /// <param name="minPageSize">The minimum page size.</param>
    /// <param name="maxPageSize">The maximum page size.</param>
    public void SetPageSizeFromViewport(
        double viewportHeight,
        double estimatedItemHeight,
        int minPageSize = DefaultMinPageSize,
        int maxPageSize = DefaultMaxPageSize)
    {
        if (!double.IsFinite(viewportHeight) || viewportHeight <= 0 || !double.IsFinite(estimatedItemHeight) || estimatedItemHeight <= 0)
        {
            return;
        }

        var pageSize = (int)Math.Floor(viewportHeight / estimatedItemHeight);
        SetPageSize(Math.Clamp(pageSize, Math.Max(1, minPageSize), Math.Max(minPageSize, maxPageSize)));
    }

    /// <summary>
    /// Sets the page size and refreshes the current page.
    /// </summary>
    /// <param name="pageSize">The new page size.</param>
    public void SetPageSize(int pageSize)
    {
        var normalizedPageSize = Math.Max(DefaultMinPageSize, pageSize);
        if (normalizedPageSize == PageSize)
        {
            return;
        }

        PageSize = normalizedPageSize;
        ClampPageIndex();
        RefreshCurrentPage();
        RaisePagingPropertiesChanged();
    }

    private void MovePrevious()
    {
        if (!CanMovePrevious)
        {
            return;
        }

        PageIndex--;
        RefreshCurrentPage();
    }

    private void MoveNext()
    {
        if (!CanMoveNext)
        {
            return;
        }

        PageIndex++;
        RefreshCurrentPage();
    }

    private void RefreshCurrentPage()
    {
        Items.Clear();
        foreach (var item in _sourceItems.Skip(PageIndex * PageSize).Take(PageSize))
        {
            Items.Add(item);
        }

        RaisePagingPropertiesChanged();
    }

    private void ClampPageIndex()
    {
        if (PageCount == 0)
        {
            _pageIndex = 0;
            return;
        }

        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
    }

    private void RaisePagingPropertiesChanged()
    {
        OnPropertyChanged(nameof(PageIndex));
        OnPropertyChanged(nameof(PageSize));
        OnPropertyChanged(nameof(TotalItemCount));
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(CanMovePrevious));
        OnPropertyChanged(nameof(CanMoveNext));
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PageText));
        _previousPageCommand.RaiseCanExecuteChanged();
        _nextPageCommand.RaiseCanExecuteChanged();
    }
}
