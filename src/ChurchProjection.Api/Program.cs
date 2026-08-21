using ChurchProjection.Api;
using ChurchProjection.Api.Endpoints;
using ChurchProjection.Api.Live;

var builder = WebApplication.CreateBuilder(args);

builder.AddProjection();
builder.Services.AddWindowsService();

var app = builder.Build();

await app.PrepareDatabaseAsync();

app.UseRateLimiter();
app.MapAccess();
app.MapBible();
app.MapSongs();
app.MapImport();
app.MapServices();
app.MapMedia();
app.MapLive();
app.MapHub<LiveHub>("/hub/live");

app.MapGet("/healthz", () => Results.Json(new
{
    ok = true,
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
}));

app.Run();

// ProjectionAppFactory is a WebApplicationFactory<Program>, and top-level
// statements generate an internal Program. This makes it visible to the test
// project without an InternalsVisibleTo.
public partial class Program;
