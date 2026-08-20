var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

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
