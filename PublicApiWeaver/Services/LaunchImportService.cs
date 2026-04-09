using Microsoft.EntityFrameworkCore;
using PublicApiWeaver.Data;
using PublicApiWeaver.Models;

namespace PublicApiWeaver.Services;

public sealed class LaunchImportService(ISpaceXClient spaceXClient, LaunchDbContext dbContext)
{
    public async Task<ImportSummary> ImportUpcomingAsync(int limit, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var fetched = await spaceXClient.GetUpcomingLaunchesAsync(cancellationToken);

        var normalized = fetched
            .Where(x => x.Upcoming)
            .OrderBy(x => x.DateUtc ?? DateTimeOffset.MaxValue)
            .Take(limit)
            .Select(x => new SpaceLaunch
            {
                ExternalId = x.Id,
                MissionName = x.Name,
                LaunchDateUtc = x.DateUtc,
                Status = "upcoming",
                Launchpad = x.Launchpad,
                HasWebcast = !string.IsNullOrWhiteSpace(x.Links?.Webcast),
                WebcastUrl = x.Links?.Webcast,
                WatchScore = CalculateWatchScore(x.DateUtc, x.Links?.Webcast),
                ImportedAtUtc = now
            })
            .ToList();

        var existing = await dbContext.Launches
            .Where(x => normalized.Select(n => n.ExternalId).Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, cancellationToken);

        var inserted = 0;
        var updated = 0;

        foreach (var launch in normalized)
        {
            if (existing.TryGetValue(launch.ExternalId, out var dbLaunch))
            {
                dbLaunch.MissionName = launch.MissionName;
                dbLaunch.LaunchDateUtc = launch.LaunchDateUtc;
                dbLaunch.Status = launch.Status;
                dbLaunch.Launchpad = launch.Launchpad;
                dbLaunch.HasWebcast = launch.HasWebcast;
                dbLaunch.WebcastUrl = launch.WebcastUrl;
                dbLaunch.WatchScore = launch.WatchScore;
                dbLaunch.ImportedAtUtc = now;
                updated++;
            }
            else
            {
                dbContext.Launches.Add(launch);
                inserted++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportSummary
        {
            RequestedLimit = limit,
            FetchedFromApi = fetched.Count,
            Processed = normalized.Count,
            Inserted = inserted,
            Updated = updated
        };
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var next30 = now.AddDays(30);
        var launches = await dbContext.Launches.AsNoTracking().ToListAsync(cancellationToken);

        var total = launches.Count;
        var withWebcast = launches.Count(x => x.HasWebcast);
        var nextLaunch = launches
            .Where(x => x.LaunchDateUtc is not null && x.LaunchDateUtc >= now)
            .OrderBy(x => x.LaunchDateUtc)
            .Select(x => new NextLaunchDto(x.MissionName, x.LaunchDateUtc, x.Launchpad, x.WatchScore))
            .FirstOrDefault();

        nextLaunch ??= launches
            .Where(x => x.LaunchDateUtc is not null)
            .OrderBy(x => x.LaunchDateUtc)
            .Select(x => new NextLaunchDto(x.MissionName, x.LaunchDateUtc, x.Launchpad, x.WatchScore))
            .FirstOrDefault();

        var launchesIn30Days = launches
            .Count(x => x.LaunchDateUtc is not null && x.LaunchDateUtc >= now && x.LaunchDateUtc <= next30);

        var topPads = launches
            .Where(x => x.Launchpad is not null)
            .GroupBy(x => x.Launchpad!)
            .Select(group => new LaunchpadStatDto(group.Key, group.Count()))
            .OrderByDescending(x => x.Count)
            .Take(3)
            .ToList();

        return new DashboardDto(total, withWebcast, launchesIn30Days, nextLaunch, topPads);
    }

    public async Task<IReadOnlyList<RecommendationDto>> GetRecommendationsAsync(int take, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var launches = await dbContext.Launches.AsNoTracking().ToListAsync(cancellationToken);

        var items = launches
            .Where(x => x.LaunchDateUtc is not null)
            .OrderBy(x => x.LaunchDateUtc >= now ? 0 : 1)
            .ThenByDescending(x => x.WatchScore)
            .ThenBy(x => x.LaunchDateUtc)
            .Take(take)
            .Select(x => new RecommendationDto(
                x.MissionName,
                x.LaunchDateUtc,
                x.Launchpad,
                x.HasWebcast,
                x.WebcastUrl,
                x.WatchScore))
            .ToList();

        return items;
    }

    private static int CalculateWatchScore(DateTimeOffset? launchDateUtc, string? webcastUrl)
    {
        var score = 50 + (!string.IsNullOrWhiteSpace(webcastUrl) ? 25 : 0);

        if (launchDateUtc is null)
        {
            return Math.Clamp(score - 15, 0, 100);
        }

        var days = (launchDateUtc.Value - DateTimeOffset.UtcNow).TotalDays;

        score += days switch
        {
            <= 7 => 25,
            <= 21 => 15,
            <= 60 => 5,
            _ => -10
        };

        return Math.Clamp(score, 0, 100);
    }
}

public sealed class ImportSummary
{
    public int RequestedLimit { get; init; }
    public int FetchedFromApi { get; init; }
    public int Processed { get; init; }
    public int Inserted { get; init; }
    public int Updated { get; init; }
}

public sealed record NextLaunchDto(
    string MissionName,
    DateTimeOffset? LaunchDateUtc,
    string? Launchpad,
    int WatchScore);

public sealed record LaunchpadStatDto(string Launchpad, int Count);

public sealed record DashboardDto(
    int TotalStored,
    int WithWebcast,
    int LaunchesInNext30Days,
    NextLaunchDto? NextLaunch,
    IReadOnlyList<LaunchpadStatDto> TopLaunchpads);

public sealed record RecommendationDto(
    string MissionName,
    DateTimeOffset? LaunchDateUtc,
    string? Launchpad,
    bool HasWebcast,
    string? WebcastUrl,
    int WatchScore);
