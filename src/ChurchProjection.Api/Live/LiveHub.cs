// src/ChurchProjection.Api/Live/LiveHub.cs
using ChurchProjection.Api.Access;
using ChurchProjection.Application.Live;

using Microsoft.AspNetCore.SignalR;

namespace ChurchProjection.Api.Live;

public sealed class LiveHub(
    LiveCommandHandler handler, OutputCounter outputs) : Hub
{
    private const string RoleKey = "role";

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

        // The role decided here is the only record of it. SendCommand reads it
        // back rather than re-reading the query string, so a connection cannot
        // be one role for the pair check and another for authorisation.
        Context.Items[RoleKey] = role;

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
        // FR-SEC-10 lets an output connection skip the PIN only because that
        // role can issue no command, so the role has to be checked here too —
        // OnConnectedAsync letting the socket open is not permission to drive it.
        // SendCommand is the only client-invokable method on this hub; a second
        // one must repeat this guard or the pair of them must move to an
        // IHubFilter.
        if (!Context.Items.TryGetValue(RoleKey, out var role) || role as string is not ("control" or "remote"))
        {
            throw new HubException("Only a paired control or remote connection can send commands.");
        }

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
