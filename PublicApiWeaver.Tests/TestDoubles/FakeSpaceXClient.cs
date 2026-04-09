using PublicApiWeaver.Contracts;
using PublicApiWeaver.Services;

namespace PublicApiWeaver.Tests.TestDoubles;

internal sealed class FakeSpaceXClient(IReadOnlyList<SpaceXLaunchDto> launches) : ISpaceXClient
{
    public Task<IReadOnlyList<SpaceXLaunchDto>> GetUpcomingLaunchesAsync(CancellationToken cancellationToken)
        => Task.FromResult(launches);
}