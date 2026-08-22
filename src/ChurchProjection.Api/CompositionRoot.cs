using ChurchProjection.Api.Options;
using ChurchProjection.Application.Import;
using ChurchProjection.Application.Live;
using ChurchProjection.Application.Ports;
using ChurchProjection.Infrastructure.Caching;
using ChurchProjection.Infrastructure.Import;
using ChurchProjection.Infrastructure.Persistence;
using ChurchProjection.Infrastructure.Repositories;

using System.Threading.RateLimiting;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChurchProjection.Api;

public static class CompositionRoot
{
    public static void AddProjection(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.Section))
            .PostConfigure<IHostEnvironment>((options, environment) =>
                options.MakeAbsolute(environment.ContentRootPath));

        builder.Services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.Section));

        builder.Services.AddOptions<AccessOptions>()
            .Bind(configuration.GetSection(AccessOptions.Section))
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<AccessOptions>>(
            new TestSettingsRefusedInProduction(builder.Environment));

        var storage = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();

        storage.MakeAbsolute(builder.Environment.ContentRootPath);
        var cache = configuration.GetSection(CacheOptions.Section).Get<CacheOptions>() ?? new CacheOptions();
        var access = configuration.GetSection(AccessOptions.Section).Get<AccessOptions>() ?? new AccessOptions();

        // Resolved per scope, not here: WebApplicationFactory layers its test
        // configuration on after AddProjection has run, so an eagerly captured
        // path would send the tests at the developer own database.
        builder.Services.AddDbContext<ProjectionDbContext>((provider, options) =>
            options.UseSqlite(
                $"Data Source={provider.GetRequiredService<IOptions<StorageOptions>>().Value.DatabasePath}"));

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
        // Per scope, for the same reason the DbContext is: the media root is not
        // known until the final configuration is in place.
        builder.Services.AddScoped<IMediaRepository>(provider => new MediaRepository(
            provider.GetRequiredService<ProjectionDbContext>(),
            provider.GetRequiredService<IOptions<StorageOptions>>().Value.MediaRoot));
        builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
        builder.Services.AddScoped<ILiveStateRepository, LiveStateRepository>();
        builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        builder.Services.AddScoped<Access.PinService>();

        builder.Services.AddRateLimiter(limiter =>
        {
            // Two limits, and pairing has to get past both. Partitioning by
            // remote address is the right first line — NFR-SEC-05 asks that one
            // phone guessing PINs must not lock out the operator's tablet — but
            // it bounds only what one address can try, and 5 a minute is 50,400
            // a week from each of however many addresses a laptop cares to bind.
            limiter.AddPolicy("pair", http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = access.PairAttemptsPerWindow,
                    Window = access.PairWindow,
                    QueueLimit = 0,
                }));

            // The backstop that makes the total finite. Scoped to the pairing
            // route by hand because the global limiter otherwise sees every
            // request in the app, and nothing else here wants throttling.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                http.Request.Path.StartsWithSegments("/api/pair")
                    ? RateLimitPartition.GetFixedWindowLimiter("pair", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = access.PairAttemptsPerGlobalWindow,
                        Window = access.PairGlobalWindow,
                        QueueLimit = 0,
                    })
                    : RateLimitPartition.GetNoLimiter<string>("unlimited"));

            limiter.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                // Set explicitly: the framework does not add Retry-After for a
                // fixed window, and rate-limit-pairing.bru asserts it is there.
                // Read off the lease so a global rejection quotes the global
                // window rather than the shorter per-address one.
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window)
                    ? window
                    : access.PairWindow;

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString();

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

        builder.Services.AddSignalR();
        builder.Services.AddSingleton<Live.OutputCounter>();
        builder.Services.AddScoped<ContentResolver>();
        builder.Services.AddScoped<LiveCommandHandler>();

        // Ticket cookies must survive a restart, or every restart un-pairs the
        // whole team mid-service.
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storage.DatabasePath))!, "keys")));
    }

    /// <summary>
    /// A test convenience that survives into a real start is not a test
    /// convenience, it is a hole. Written as an <see cref="IValidateOptions{T}"/>
    /// so it judges the same instance the pair gate resolves per request, rather
    /// than a snapshot bound at composition time that a reload could leave
    /// behind. <c>ValidateOnStart</c> keeps the failure at startup, so the server
    /// still refuses to start rather than starting wrong.
    /// </summary>
    private sealed class TestSettingsRefusedInProduction(IHostEnvironment environment)
        : IValidateOptions<AccessOptions>
    {
        public ValidateOptionsResult Validate(string? name, AccessOptions options)
        {
            if (!environment.IsProduction())
            {
                return ValidateOptionsResult.Skip;
            }

            if (!string.IsNullOrWhiteSpace(options.TestPin))
            {
                return ValidateOptionsResult.Fail(
                    "Access:TestPin is a test-only setting and is refused in Production.");
            }

            if (options.RequirePairingFromLoopback)
            {
                return ValidateOptionsResult.Fail(
                    "Access:RequirePairingFromLoopback is a test-only setting and is refused in Production.");
            }

            return ValidateOptionsResult.Success;
        }
    }

    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(storage.DatabasePath))!);
        Directory.CreateDirectory(storage.MediaRoot);

        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();

        await db.ApplyMigrationsAsync(CancellationToken.None);

        if (app.Environment.EnvironmentName == "Testing")
        {
            await DevSeed.ApplyAsync(db, CancellationToken.None);
        }
    }
}
