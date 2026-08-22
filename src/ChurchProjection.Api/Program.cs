using ChurchProjection.Api;
using ChurchProjection.Api.Endpoints;
using ChurchProjection.Api.Live;

using Microsoft.Extensions.Hosting.WindowsServices;

// AddWindowsService() sets the host lifetime and nothing else. A service
// started by sc.exe runs with the working directory C:\Windows\System32, so
// without this the booth's appsettings.json is never read and the data folder
// the runbook tells the volunteer to back up is never written to. Left at the
// default off the service path, which is what keeps WebApplicationFactory free
// to point the content root at its own test root.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
});

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
