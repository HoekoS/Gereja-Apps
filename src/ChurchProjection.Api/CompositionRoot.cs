using ChurchProjection.Api.Options;
using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Infrastructure.Caching;
using ChurchProjection.Infrastructure.Import;
using ChurchProjection.Infrastructure.Persistence;
using ChurchProjection.Infrastructure.Repositories;

using System.Threading.RateLimiting;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Api;

public static class CompositionRoot
{
    public static void AddProjection(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        builder.Services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.Section));
        builder.Services.Configure<AccessOptions>(configuration.GetSection(AccessOptions.Section));

        var storage = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
        var cache = configuration.GetSection(CacheOptions.Section).Get<CacheOptions>() ?? new CacheOptions();
        var access = configuration.GetSection(AccessOptions.Section).Get<AccessOptions>() ?? new AccessOptions();

        RefuseTestSettingsInProduction(builder.Environment, access);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(storage.DatabasePath))!);
        Directory.CreateDirectory(storage.MediaRoot);

        builder.Services.AddDbContext<ProjectionDbContext>(options =>
            options.UseSqlite($"Data Source={storage.DatabasePath}"));

        if (string.IsNullOrWhiteSpace(cache.Redis.ConnectionString))
        {
            // INT-13: no cache configured is a supported configuration, not a
            // degraded one. The booth normally runs exactly like this.
            builder.Services.AddDistributedMemoryCache();
        }
        else
        {
            builder.Services.AddStackExchangeRedisCache(options =>
                options.Configuration = cache.Redis.ConnectionString);
        }

        builder.Services.AddSingleton<CacheGeneration>();

        builder.Services.AddScoped<ITranslationRepository, TranslationRepository>();
        builder.Services.AddScoped<ISongRepository, SongRepository>();
        builder.Services.AddScoped<IMediaRepository, MediaRepository>();
        builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
        builder.Services.AddScoped<ILiveStateRepository, LiveStateRepository>();
        builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        builder.Services.AddScoped<Access.PinService>();

        builder.Services.AddRateLimiter(limiter =>
        {
            // Partitioned by remote address, not global: NFR-SEC-05 asks that one
            // phone guessing PINs must not lock out the operator's tablet, and a
            // named fixed-window limiter is a single shared partition.
            limiter.AddPolicy("pair", http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = access.PairAttemptsPerWindow,
                    Window = access.PairWindow,
                    QueueLimit = 0,
                }));

            limiter.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                // Set explicitly: the framework does not add Retry-After for a
                // fixed window, and rate-limit-pairing.bru asserts it is there.
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)access.PairWindow.TotalSeconds).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ApiError(new ApiError.Body("TOO_MANY_ATTEMPTS", "Too many PIN attempts. Wait a minute.")),
                    ct);
            };
        });

        // The decorator, not the EF repository, is what the application resolves.
        builder.Services.AddScoped<VerseRepository>();
        builder.Services.AddScoped<IVerseRepository>(provider => new CachedVerseRepository(
            provider.GetRequiredService<VerseRepository>(),
            provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
            provider.GetRequiredService<CacheGeneration>(),
            provider.GetRequiredService<ILogger<CachedVerseRepository>>()));

        builder.Services.AddSingleton<IImportReader>(_ => ImportService.WithDefaultParsers());
        builder.Services.AddScoped<ImportLibrary>();

        // Ticket cookies must survive a restart, or every restart un-pairs the
        // whole team mid-service.
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storage.DatabasePath))!, "keys")));
    }

    /// <summary>
    /// A test convenience that survives into a real start is not a test
    /// convenience, it is a hole. Refusing at composition time means the server
    /// will not start rather than starting wrong.
    /// </summary>
    private static void RefuseTestSettingsInProduction(IHostEnvironment environment, AccessOptions access)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(access.TestPin))
        {
            throw new InvalidOperationException(
                "Access:TestPin is a test-only setting and is refused in Production.");
        }

        if (access.RequirePairingFromLoopback)
        {
            throw new InvalidOperationException(
                "Access:RequirePairingFromLoopback is a test-only setting and is refused in Production.");
        }
    }

    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();

        await db.ApplyMigrationsAsync(CancellationToken.None);

        if (app.Environment.EnvironmentName == "Testing")
        {
            await DevSeed.ApplyAsync(db, CancellationToken.None);
        }
    }
}
