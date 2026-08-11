namespace SiteQueryDefectTracking.Application.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Empty(int page = 1, int pageSize = Pagination.DefaultPageSize) =>
        new() { Items = Array.Empty<T>(), TotalCount = 0, Page = page, PageSize = pageSize };

    public static PagedResult<T> Create(IEnumerable<T> items, int totalCount, int page, int pageSize) =>
        new() { Items = items.ToList(), TotalCount = totalCount, Page = page, PageSize = pageSize };
}