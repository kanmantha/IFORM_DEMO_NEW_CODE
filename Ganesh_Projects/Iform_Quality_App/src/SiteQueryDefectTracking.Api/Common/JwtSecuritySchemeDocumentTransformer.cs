using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SiteQueryDefectTracking.Api.Common;

/// <summary>
/// Documents the API metadata in the generated OpenAPI document.
/// </summary>
public sealed class JwtSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Site Query & Defect Tracking API",
            Version = "v1",
            Description = "IFORM site query and defect tracking API."
        };

        return Task.CompletedTask;
    }
}