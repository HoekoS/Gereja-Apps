using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Persistence;

public sealed class ProjectionDbContext(DbContextOptions<ProjectionDbContext> options) : DbContext(options)
{
    public DbSet<Translation> Translations => Set<Translation>();

    public DbSet<BookNameRow> BookNames => Set<BookNameRow>();

    public DbSet<Verse> Verses => Set<Verse>();

    public DbSet<Song> Songs => Set<Song>();

    public DbSet<MediaItem> Media => Set<MediaItem>();

    public DbSet<ServicePlan> Services => Set<ServicePlan>();

    public DbSet<SettingRow> Settings => Set<SettingRow>();

    public DbSet<LiveStateRow> LiveState => Set<LiveStateRow>();

    public Task ApplyMigrationsAsync(CancellationToken ct) => Database.MigrateAsync(ct);

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.ApplyConfigurationsFromAssembly(typeof(ProjectionDbContext).Assembly);
}
