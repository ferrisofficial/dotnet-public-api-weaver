using Microsoft.EntityFrameworkCore;
using PublicApiWeaver.Contracts;
using PublicApiWeaver.Data;
using PublicApiWeaver.Services;

namespace PublicApiWeaver.Tests.TestDoubles;

internal static class ServiceFactory
{
    public static LaunchImportService CreateService(IReadOnlyList<SpaceXLaunchDto> launches, out LaunchDbContext dbContext)
    {
        var options = new DbContextOptionsBuilder<LaunchDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}")
            .Options;

        dbContext = new LaunchDbContext(options);
        var client = new FakeSpaceXClient(launches);
        return new LaunchImportService(client, dbContext);
    }
}