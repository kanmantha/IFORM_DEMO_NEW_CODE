namespace IForm.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize, int TotalPages)
{
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public static PagedResult<T> Empty(int page = 1, int pageSize = 10) =>
        new(Array.Empty<T>(), 0, page, pageSize, 0);
}

public static class Paging
{
    public static PagedResult<T> ToPaged<T>(this IEnumerable<T> source, int page, int pageSize)
    {
        var items = source.ToList();
        var total = items.Count;
        var pages = pageSize <= 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResult<T>(items, total, page, pageSize, pages);
    }

    public static PagedResult<T> ToPaged<T>(this IQueryable<T> source, int page, int pageSize)
    {
        var total = source.Count();
        var pages = pageSize <= 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
        var items = source.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<T>(items, total, page, pageSize, pages);
    }
}
