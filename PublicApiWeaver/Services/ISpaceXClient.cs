using PublicApiWeaver.Contracts;

namespace PublicApiWeaver.Services;

public interface ISpaceXClient
{
    Task<IReadOnlyList<SpaceXLaunchDto>> GetUpcomingLaunchesAsync(CancellationToken cancellationToken);
}