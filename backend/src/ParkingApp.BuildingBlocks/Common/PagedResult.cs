namespace ParkingApp.BuildingBlocks.Common;

/// <summary>
/// Paginated result wrapper for list queries
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    
    public PagedResult(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items.ToList().AsReadOnly();
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
    
    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 10)
    {
        return new PagedResult<T>(Enumerable.Empty<T>(), 0, pageNumber, pageSize);
    }
    
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        return new PagedResult<TOut>(Items.Select(mapper), TotalCount, PageNumber, PageSize);
    }
}

/// <summary>
/// Query parameters for pagination
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 10;
    
    private int _pageNumber = 1;
    private int _pageSize = DefaultPageSize;
    
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }
    
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? DefaultPageSize : value;
    }
    
    public int Skip => (PageNumber - 1) * PageSize;
}
