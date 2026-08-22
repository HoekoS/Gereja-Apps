using ChurchProjection.Api.Access;
using ChurchProjection.Application.Import;

using Microsoft.AspNetCore.Http.Features;

namespace ChurchProjection.Api.Endpoints;

public static class ImportEndpoints
{
    /// <summary>NFR-SEC-06. A Bible is a few megabytes; a hundred is an attack.</summary>
    private const long MaxUploadBytes = 100L * 1024 * 1024;

    public static void MapImport(this WebApplication app)
    {
        app.MapPost("/api/import", async (HttpRequest request, ImportLibrary import, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return ApiError.BadRequest("NOT_MULTIPART", "Send the file as multipart/form-data.");
            }

            // Kestrel's own default is 30 MB, so without this the check below is
            // unreachable and a legitimate upload dies with a raw framework 413
            // instead of the contract-shaped one. Raised per request rather than
            // globally so only the upload routes are this open.
            var body = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

            if (body is { IsReadOnly: false })
            {
                body.MaxRequestBodySize = MaxUploadBytes;
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
            {
                return ApiError.BadRequest("NO_FILE", "No file was attached to the import.");
            }

            if (file.Length > MaxUploadBytes)
            {
                return ApiError.Result(413, "FILE_TOO_LARGE", "That file is larger than the import limit.");
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var outcome = await import.ExecuteAsync(stream, file.FileName, ct);

                return Results.Json(new
                {
                    kind = outcome.Kind,
                    imported = outcome.Imported,
                    updated = outcome.Updated,
                });
            }
            catch (ImportException ex)
            {
                // The message names the record that failed, because "Import
                // failed" is not something to hand a volunteer at nine o'clock
                // on a Saturday night (FR-ADM-02).
                return ApiError.Result(422, "IMPORT_REJECTED", ex.Detail);
            }
        })
        .RequirePair()
        .DisableAntiforgery();
    }
}
