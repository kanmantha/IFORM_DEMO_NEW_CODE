namespace SiteQueryDefectTracking.Domain.Constants;

public static class AppRoles
{
    public const string Manager = "Manager";
    public const string SiteEngineer = "Site Engineer";

    public static readonly IReadOnlyList<string> All = new[] { Manager, SiteEngineer };
}

public static class AppPolicies
{
    public const string RequireManager = "RequireManager";
    public const string CanResolveQueries = "CanResolveQueries";
    public const string CanManageCatalogue = "CanManageCatalogue";
    public const string CanViewDashboard = "CanViewDashboard";
    public const string CanManageEmails = "CanManageEmails";
    public const string CanViewAuditLogs = "CanViewAuditLogs";
}

public static class IssueTypeCodes
{
    public const string Missing = "MISSING";
    public const string ProductionMistake = "PRODUCTION_MISTAKE";
    public const string DesignMistake = "DESIGN_MISTAKE";
    public const string DispatchMissing = "DISPATCH_MISSING";

    public static readonly IReadOnlyList<string> All =
        new[] { Missing, ProductionMistake, DesignMistake, DispatchMissing };

    public static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>
        {
            [Missing] = "Missing",
            [ProductionMistake] = "Production Mistake",
            [DesignMistake] = "Design Mistake",
            [DispatchMissing] = "Dispatch Missing"
        };
}

public static class EmailTemplateCodes
{
    public const string Missing = "TEMPLATE_MISSING";
    public const string ProductionMistake = "TEMPLATE_PRODUCTION_MISTAKE";
    public const string DesignMistake = "TEMPLATE_DESIGN_MISTAKE";
    public const string DispatchMissing = "TEMPLATE_DISPATCH_MISSING";
}

public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string RefreshTokenUsed = "RefreshTokenUsed";
    public const string QueryCreated = "QueryCreated";
    public const string QueryUpdated = "QueryUpdated";
    public const string QueryStatusChanged = "QueryStatusChanged";
    public const string QueryResolved = "QueryResolved";
    public const string CommentAdded = "CommentAdded";
    public const string AttachmentUploaded = "AttachmentUploaded";
    public const string CatalogueUploaded = "CatalogueUploaded";
    public const string ProductModified = "ProductModified";
    public const string EmailGenerated = "EmailGenerated";
    public const string EmailSent = "EmailSent";
    public const string EmailFailed = "EmailFailed";
    public const string EmailTemplateModified = "EmailTemplateModified";
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
}

public static class ConfigurationKeys
{
    public const string DefaultConnection = "ConnectionStrings:DefaultConnection";
    public const string Jwt = "Jwt";
    public const string Email = "Email";
    public const string Storage = "Storage";
    public const string FileUpload = "FileUpload";
    public const string Application = "Application";
    public const string Seed = "Seed";
    public const string Frontend = "Frontend";
}

public static class AppDefaults
{
    public const string TimeZoneId = "Asia/Kolkata";
    public const int Page = 1;
    public const int PageSize = 25;
    public const int MaxPageSize = 100;
    public const long MaxFileSizeBytes = 10L * 1024 * 1024;
    public const int PhotoRetentionDays = 365;
    public const string AttachmentPhotoCategory = "Photo";
    public const string AttachmentDocumentCategory = "Document";
}

public static class DelayThresholds
{
    public const int Minor = 3;
    public const int Moderate = 7;
    public const int Critical = 14;
}