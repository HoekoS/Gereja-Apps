using ChurchProjection.Api.Access;
using ChurchProjection.Api.Options;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Storage;

using Microsoft.Extensions.Options;

namespace ChurchProjection.Api.Endpoints;

public static class MediaEndpoints
{
    private const long MaxUploadBytes = 500L * 1024 * 1024;

    public static void MapMedia(this WebApplication app)
    {
        var group = app.MapGroup("/api/media").RequirePair();

        group.MapGet("/", async (
            IMediaRepository media, IOptions<StorageOptions> storage, CancellationToken ct) =>
        {
            var all = await media.ListAsync(ct);

            return Results.Json(new { results = all.Select(item => Describe(item, storage.Value.MediaRoot)) });
        });

        group.MapGet("/{id}", async (
            string id, IMediaRepository media, IOptions<StorageOptions> storage, CancellationToken ct) =>
        {
            var item = await media.FindAsync(new MediaId(id), ct);

            return item is null
                ? ApiError.NotFound("MEDIA_NOT_FOUND", "That media item is not in the library.")
                : Results.Json(Describe(item, storage.Value.MediaRoot));
        });

        group.MapGet("/{id}/stream", async (
            HttpResponse response,
            string id,
            IMediaRepository media,
            IOptions<StorageOptions> storage,
            CancellationToken ct) =>
        {
            var item = await media.FindAsync(new MediaId(id), ct);

            if (item is null)
            {
                return ApiError.NotFound("MEDIA_NOT_FOUND", "That media item is not in the library.");
            }

            // The caller names a database id, never a path. Resolve is a second
            // line of defence in case a row's stored filename is ever wrong.
            if (MediaPaths.Resolve(storage.Value.MediaRoot, item.Filename) is not { } path || !File.Exists(path))
            {
                return ApiError.NotFound("MEDIA_FILE_MISSING", $"'{item.Filename}' is not in the media folder.");
            }

            // Served as what the extension says, plus nosniff, so a row that
            // predates the upload whitelist cannot get HTML executed on this
            // origin — the origin that holds the pair cookie.
            response.Headers.XContentTypeOptions = "nosniff";

            // enableRangeProcessing is what makes the projector's video element
            // able to seek instead of buffering the whole file first.
            return Results.File(
                path,
                MediaPaths.ContentTypeFor(item.Filename) ?? "application/octet-stream",
                enableRangeProcessing: true);
        });

        group.MapPost("/", async (
            HttpRequest request,
            IMediaRepository media,
            IOptions<StorageOptions> storage,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return ApiError.BadRequest("NOT_MULTIPART", "Send the file as multipart/form-data.");
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
            {
                return ApiError.BadRequest("NO_FILE", "No file was attached.");
            }

            if (file.Length > MaxUploadBytes)
            {
                return ApiError.Result(413, "FILE_TOO_LARGE", "That file is larger than the upload limit.");
            }

            var name = MediaPaths.Sanitise(file.FileName);

            if (string.IsNullOrWhiteSpace(name) ||
                MediaPaths.Resolve(storage.Value.MediaRoot, name) is not { } destination)
            {
                return ApiError.BadRequest("BAD_FILENAME", "That filename cannot be stored.");
            }

            // Derived from the extension, never from the client's Content-Type
            // header: the header is attacker-chosen and an accepted text/html
            // upload is stored XSS on the origin that holds the pair cookie.
            if (MediaPaths.ContentTypeFor(name) is not { } kind)
            {
                return ApiError.BadRequest(
                    "UNSUPPORTED_FILE_TYPE", $"'{name}' is not an image, video or audio file this app can project.");
            }

            try
            {
                await using (var target = File.Create(destination))
                {
                    await file.CopyToAsync(target, ct);
                }
            }
            catch (IOException)
            {
                // INT-05: a full disk must not leave a half file behind with a
                // row pointing at it.
                File.Delete(destination);

                return ApiError.Result(507, "STORAGE_FULL", "There is not enough space to store that file.");
            }

            var item = new MediaItem
            {
                Id = $"med_{Guid.NewGuid():n}"[..12],
                Kind = kind,
                Filename = name,
            };

            await media.AddAsync(item, ct);

            return Results.Created($"/api/media/{item.Id.Value}", Describe(item, storage.Value.MediaRoot));
        })
        .DisableAntiforgery();
    }

    private static object Describe(MediaItem item, string mediaRoot) => new
    {
        id = item.Id.Value,
        kind = item.Kind,
        filename = item.Filename,
        durationMs = item.DurationMs,
        width = item.Width,
        height = item.Height,

        // Checked on every read rather than stored, because the failure this
        // guards against is someone tidying the media folder between Saturday
        // and Sunday (FR-LIB-23). Same call the repository makes, against the
        // same root, so the two cannot disagree.
        available = MediaPaths.Exists(mediaRoot, item.Filename),
    };
}
