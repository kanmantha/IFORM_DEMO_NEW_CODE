using IformSiteQuery.Domain.Constants;
using IformSiteQuery.Domain.Entities;
using IformSiteQuery.Domain.Enums;

namespace IformSiteQuery.Infrastructure.Email;

public record EmailDraft(string Subject, string Body);

public static class EmailTemplateService
{
    public static EmailDraft Build(SiteQuery query, string senderName, string senderEmail, string? managerName = null)
    {
        var projectName = query.Project?.Name ?? "N/A";
        var issue = IssueTypeDisplay.GetName(query.IssueType);
        var subject = $"[IFORM] {issue} - IPO {query.IpoNumber} - {projectName}";

        var managerGreeting = string.IsNullOrWhiteSpace(managerName) ? "Team" : managerName;
        var summary =
            $"IPO Number: {query.IpoNumber}\n" +
            $"Project: {projectName}\n" +
            $"Issue Type: {issue}\n" +
            $"Quantity: {query.QtyNos:N0} nos / {query.QtySqm:N2} sqm\n" +
            $"Query No.: {query.QueryNumber}\n" +
            $"Raised On: {query.RaisedAt:dd/MM/yyyy}";

        var body = query.IssueType switch
        {
            IssueType.Missing =>
                $"Dear {managerGreeting},\n\n" +
                "We have identified missing items at site as detailed below:\n\n" +
                summary +
                $"\n\nPlease arrange the dispatch of the above missing items at the earliest.\n\n" +
                $"Thanks & Regards,\n{senderName}\n{senderEmail}",
            IssueType.ProductionMistake =>
                $"Dear {managerGreeting},\n\n" +
                "A production mistake has been identified at site as detailed below:\n\n" +
                summary +
                $"\n\nKindly advise the corrective action and replacement schedule.\n\n" +
                $"Thanks & Regards,\n{senderName}\n{senderEmail}",
            IssueType.DesignMistake =>
                $"Dear {managerGreeting},\n\n" +
                "A design mistake has been identified at site as detailed below:\n\n" +
                summary +
                $"\n\nRequest the design team to review and provide the revised details at the earliest.\n\n" +
                $"Thanks & Regards,\n{senderName}\n{senderEmail}",
            _ =>
                $"Dear {managerGreeting},\n\n" +
                "The following item(s) were found missing during dispatch as detailed below:\n\n" +
                summary +
                $"\n\nKindly verify the dispatch records and arrange delivery at the earliest.\n\n" +
                $"Thanks & Regards,\n{senderName}\n{senderEmail}"
        };

        return new EmailDraft(subject, body);
    }
}
