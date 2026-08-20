// Integration tests for the live channel (TEST-CASES INT-07, INT-08, INT-13,
// SYS-LIV-13).
//
// This is the coverage Bruno cannot reach. Bruno speaks HTTP; it cannot hold a
// second connection open and assert that a command issued on one client arrives
// at another. That broadcast is the whole reason the server is authoritative, so
// it is tested here rather than left to the manual pass.

using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.SignalR.Client;

using Xunit;

namespace ChurchProjection.Api.Tests;

public class LiveBroadcastTests(ProjectionAppFactory factory) : IClassFixture<ProjectionAppFactory>
{
    private HubConnection ConnectOutput()
    {
        var handler = factory.Server.CreateHandler();

        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hub/live?role=output"), options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
            })
            .Build();
    }

    [Fact]
    public async Task INT_08_CRITICAL_a_command_reaches_a_second_client()
    {
        var client = factory.CreateClient();
        await PairAsync(client);

        await using var output = ConnectOutput();

        var received = new TaskCompletionSource<LiveStateDto>();
        output.On<LiveStateDto>("StateChanged", state => received.TrySetResult(state));
        await output.StartAsync(TestContext.Current.CancellationToken);

        var itemId = await SeedOneItemServiceAsync(client);
        await client.PostAsJsonAsync("/api/live/command",
            new { type = "preview", itemId, pageIndex = 0 },
            TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync("/api/live/command",
            new { type = "go" },
            TestContext.Current.CancellationToken);

        var state = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(itemId, state.Live?.ItemId);
    }

    [Fact]
    public async Task INT_07_CRITICAL_a_client_receives_full_state_on_connect()
    {
        // A control view that reloads mid-service must recover everything from
        // the connection itself, with no extra request and no operator action.
        var client = factory.CreateClient();
        await PairAsync(client);

        await using var output = ConnectOutput();

        var received = new TaskCompletionSource<LiveStateDto>();
        output.On<LiveStateDto>("StateChanged", state => received.TrySetResult(state));

        await output.StartAsync(TestContext.Current.CancellationToken);

        var state = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(state.Skipped);
        Assert.False(state.Blackout);
    }

    [Fact]
    public async Task SYS_LIV_13_an_unrecognised_command_type_is_refused()
    {
        // The C# aggregate has no dynamic dispatch, so "unknown command" cannot
        // exist below this layer. It can still arrive as JSON from a stale
        // client, and it must be refused rather than silently ignored — silence
        // would leave the operator pressing a key that does nothing.
        var client = factory.CreateClient();
        await PairAsync(client);

        var response = await client.PostAsJsonAsync("/api/live/command",
            new { type = "nonsense" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task INT_13_CRITICAL_the_host_starts_with_no_cache_configured()
    {
        // The Redis connection string is deliberately unset in the test host.
        // Startup reaching this point at all is the assertion: a cache must
        // never be a precondition for the app running.
        var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task PairAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/pair",
            new { pin = ProjectionAppFactory.TestPin },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> SeedOneItemServiceAsync(HttpClient client)
    {
        var service = await client.PostAsJsonAsync("/api/services",
            new { name = "Integration", serviceDate = "2026-08-23" },
            TestContext.Current.CancellationToken);
        var serviceId = (await service.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var item = await client.PostAsJsonAsync($"/api/services/{serviceId}/items",
            new
            {
                kind = "bible",
                label = "Pembacaan",
                @ref = new { translationId = "tb", bookId = 43, chapter = 3, verseStart = 16, verseEnd = 16 },
            },
            TestContext.Current.CancellationToken);

        return (await item.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private sealed record IdDto(string Id);

    private sealed record SlotDto(string ItemId, int PageIndex);

    private sealed record LiveStateDto(
        SlotDto? Live,
        SlotDto? Preview,
        bool Blackout,
        IReadOnlyList<string> Skipped,
        int OutputsConnected);
}
