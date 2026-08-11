namespace IformSiteQuery.Domain.Entities;

public class QueryComment
{
    public int Id { get; set; }
    public int QueryId { get; set; }
    public SiteQuery? Query { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
