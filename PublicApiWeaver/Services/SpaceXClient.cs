using System.Net.Http.Json;
using PublicApiWeaver.Contracts;

namespace PublicApiWeaver.Services;

public sealed class SpaceXClient(HttpClient httpClient) : ISpaceXClient
{
    public async Task<IReadOnlyList<SpaceXLaunchDto>> GetUpcomingLaunchesAsync(CancellationToken cancellationToken)
    {
        var launches = await httpClient.GetFromJsonAsync<List<SpaceXLaunchDto>>("launches/upcoming", cancellationToken);
        return launches ?? [];
    }
}
