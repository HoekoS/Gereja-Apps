// src/ChurchProjection.Api/Live/LiveHub.cs
using ChurchProjection.Api.Access;
using ChurchProjection.Application.Live;

using Microsoft.AspNetCore.SignalR;

namespace ChurchProjection.Api.Live;

public sealed class LiveHub(
    LiveCommandHandler handler, OutputCounter outputs) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        var role = http.Request.Query["role"].ToString();

        if (role is not ("control" or "output" or "remote"))
        {
            Context.Abort();

            return;
        }

        // FR-SEC-10: the projector is a screen in a locked booth with no
        // controls; making a volunteer type a PIN into it before the service
        // starts buys nothing. Everything that can change the screen still pairs.
        if (role != "output" && !await PairGate.IsPairedAsync(http))
        {
            Context.Abort();

            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, role);

        if (role == "output")
        {
            outputs.Increment();
        }

        // FR-LIV-12: full state first, before anything else, so a client that
        // joins mid-service is correct immediately rather than after the next
        // command.
        await Clients.Caller.SendAsync("StateChanged", await StateAsync());

        if (role == "output")
        {
            await BroadcastAsync();
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.GetHttpContext()?.Request.Query["role"].ToString() == "output")
        {
            outputs.Decrement();
            await BroadcastAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendCommand(LiveCommand command)
    {
        _ = await handler.ExecuteAsync(command, Context.ConnectionAborted);

        // Refusals are not thrown at the caller here: the broadcast that follows
        // carries the unchanged state, which is what a stale control view needs
        // in order to correct itself.
        await BroadcastAsync();
    }

    private async Task<LiveStateDto> StateAsync() =>
        LiveStateDto.From(await handler.CurrentAsync(Context.ConnectionAborted), outputs.Current);

    private async Task BroadcastAsync() =>
        await Clients.All.SendAsync("StateChanged", await StateAsync());
}
