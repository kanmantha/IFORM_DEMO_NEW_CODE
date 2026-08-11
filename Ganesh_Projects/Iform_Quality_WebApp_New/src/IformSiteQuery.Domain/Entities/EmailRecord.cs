namespace IformSiteQuery.Domain.Entities;

public class EmailRecord
{
    public int Id { get; set; }
    public int? QueryId { get; set; }
    public SiteQuery? Query { get; set; }
    public string? ToAddress { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Sent { get; set; }
    public DateTime? SentAt { get; set; }
}
