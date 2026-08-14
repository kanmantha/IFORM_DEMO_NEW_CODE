using IForm.Domain.Enums;

namespace IForm.Domain.Services;

/// <summary>Pure domain rules for the EOT workflow (IFAD-POL-EOT-001).</summary>
public static class EotBusinessRules
{
    /// <summary>Net Scope Variation = Scope Addition - Scope Reduction.</summary>
    public static decimal CalculateNetScopeVariation(decimal scopeAddition, decimal scopeReduction) =>
        scopeAddition - scopeReduction;

    /// <summary>Mandatory documents required before an EOT can be submitted.</summary>
    public static readonly IReadOnlyList<DocumentCategory> RequiredDocuments =
        new[]
        {
            DocumentCategory.Drawing,             // Approved Drawings
            DocumentCategory.RevisedDrawing,      // Revised Drawings
            DocumentCategory.ClientEmail,         // Client Instructions / Emails
            DocumentCategory.DelayReport,         // Delay Analysis Report
            DocumentCategory.ScopeVariation,      // Scope Variation Statement
            DocumentCategory.InspectionReport,    // Project Progress Report
            DocumentCategory.Other                // Consultant Correspondence / Supporting
        };

    public static bool CanTransition(EotStatus current, EotStatus next)
    {
        if (current == next) return false;
        return next switch
        {
            EotStatus.Draft or EotStatus.Cancelled => true,
            EotStatus.Submitted => current == EotStatus.Draft || current == EotStatus.ReturnedForCorrection,
            EotStatus.UnderReview => current == EotStatus.Submitted,
            EotStatus.ClientSignoffPending => current == EotStatus.UnderReview,
            EotStatus.ContractsReview => current == EotStatus.ClientSignoffPending,
            EotStatus.Approved => current == EotStatus.ContractsReview,
            EotStatus.Rejected => current is EotStatus.Submitted or EotStatus.UnderReview or EotStatus.ClientSignoffPending or EotStatus.ContractsReview,
            EotStatus.ReturnedForCorrection => current == EotStatus.UnderReview || current == EotStatus.ClientSignoffPending,
            _ => false
        };
    }

    /// <summary>Validate that all required document categories are present for submission.</summary>
    public static bool HasRequiredDocuments(IEnumerable<DocumentCategory> uploadedCategories) =>
        RequiredDocuments.All(d => uploadedCategories.Contains(d));
}
