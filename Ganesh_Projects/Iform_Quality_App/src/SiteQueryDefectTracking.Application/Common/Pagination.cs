namespace SiteQueryDefectTracking.Application.Common;

public class Pagination
{
    private int _page = 1;
    private int _pageSize = 25;

    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? DefaultPage : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public (int Skip, int Take) ToSkipTake() => ((Page - 1) * PageSize, PageSize);
}