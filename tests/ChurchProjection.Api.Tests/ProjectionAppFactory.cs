// Host fixture for the integration tests (TEST-CASES INT-*).
//
// Each test class gets its own SQLite file in a temporary directory and deletes
// it on dispose. An in-memory SQLite connection would be faster, but it does not
// support FTS5 the same way a file does, and FTS5 is exactly what the search
// paths depend on — testing against something the booth will never run is worse
// than testing slowly.
//
// RED PHASE: ChurchProjection.Api does not exist yet.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ChurchProjection.Api.Tests;

public sealed class ProjectionAppFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "church-projection-tests", Guid.NewGuid().ToString("n"));

    public const string TestPin = "123456";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "media"));

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DatabasePath"] = Path.Combine(_root, "projection.db"),
                ["Storage:MediaRoot"] = Path.Combine(_root, "media"),

                // No Redis in the test host. The cache is optional by design
                // (see the backend design's Redis decision), so leaving the
                // connection string unset must select the in-memory adapter and
                // must not fail startup. If this ever throws, the "cache cannot
                // stop a service" rule has been broken.
                ["Cache:Redis:ConnectionString"] = null,

                // Test-only. Fixes the PIN so the suite need not read the
                // loopback-only pin endpoint, and switches off the loopback
                // exemption so the pair gate can actually be observed to reject.
                // Both are refused when the environment is Production.
                ["Access:TestPin"] = TestPin,
                ["Access:RequirePairingFromLoopback"] = "true",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A held SQLite handle on Windows is not a test failure. The
            // directory is under the temp path and the OS will reclaim it.
        }
    }
}
