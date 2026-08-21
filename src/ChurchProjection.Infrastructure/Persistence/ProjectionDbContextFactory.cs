using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>Exists only so `dotnet ef migrations` can run without the host.</summary>
public sealed class ProjectionDbContextFactory : IDesignTimeDbContextFactory<ProjectionDbContext>
{
    public ProjectionDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ProjectionDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options);
}
