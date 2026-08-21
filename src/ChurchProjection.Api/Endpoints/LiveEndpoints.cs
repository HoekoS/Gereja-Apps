// src/ChurchProjection.Api/Endpoints/LiveEndpoints.cs
using ChurchProjection.Api.Access;
using ChurchProjection.Api.Live;
using ChurchProjection.Application.Live;
using ChurchProjection.Domain.Live;

using Microsoft.AspNetCore.SignalR;

namespace ChurchProjection.Api.Endpoints;

public static class LiveEndpoints
{
    public static void MapLive(this WebApplication app)
    {
        var group = app.MapGroup("/api/live").RequirePair();

        group.MapGet("/", async (LiveCommandHandler handler, OutputCounter outputs, CancellationToken ct) =>
            Results.Json(LiveStateDto.From(await handler.CurrentAsync(ct), outputs.Current)));

        group.MapPost("/command", async (
            LiveCommand command,
            LiveCommandHandler handler,
            OutputCounter outputs,
            IHubContext<LiveHub> hub,
            CancellationToken ct) =>
        {
            var (result, view) = await handler.ExecuteAsync(command, ct);
            var state = LiveStateDto.From(view, outputs.Current);

            if (result.IsOk)
            {
                // The socket clients hear about an HTTP command exactly as they
                // hear about a hub command. One authority, one broadcast.
                await hub.Clients.All.SendAsync("StateChanged", state, ct);

                return Results.Json(state);
            }

            if (result.Refusal == RefusalCode.UnknownCommand)
            {
                return ApiError.BadRequest("UNKNOWN_COMMAND", $"'{command.Type}' is not a live command.");
            }

            // 409 carrying the unchanged state, so a control screen that issued
            // a stale command resyncs from the refusal itself.
            return Results.Json(
                new
                {
                    error = new { code = Code(result.Refusal), message = Message(result.Refusal) },
                    state,
                },
                statusCode: 409);
        });
    }

    private static string Code(RefusalCode refusal) => refusal switch
    {
        RefusalCode.NoPreview => "NO_PREVIEW",
        RefusalCode.NoLiveItem => "NO_LIVE_ITEM",
        RefusalCode.MediaUnavailable => "MEDIA_UNAVAILABLE",
        RefusalCode.UnknownItem => "UNKNOWN_ITEM",
        RefusalCode.PageOutOfRange => "PAGE_OUT_OF_RANGE",
        _ => "REFUSED",
    };

    private static string Message(RefusalCode refusal) => refusal switch
    {
        RefusalCode.NoPreview => "Nothing is staged, so there is nothing to send to the screen.",
        RefusalCode.NoLiveItem => "Nothing is live yet.",
        RefusalCode.MediaUnavailable => "That media file is not in the media folder.",
        RefusalCode.UnknownItem => "That item is not in the service that is running.",
        RefusalCode.PageOutOfRange => "That page is no longer part of the item.",
        _ => "That command was refused.",
    };
}
