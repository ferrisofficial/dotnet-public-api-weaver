using PublicApiWeaver.Contracts;
using PublicApiWeaver.Models;
using PublicApiWeaver.Tests.TestDoubles;

namespace PublicApiWeaver.Tests;

public sealed class LaunchImportServiceTests
{
    [Fact]
    public async Task ImportUpcomingAsync_InsertsOnlyUpcomingAndRespectsLimit()
    {
        var launches = new List<SpaceXLaunchDto>
        {
            BuildLaunch("a", DateTimeOffset.UtcNow.AddDays(2), true),
            BuildLaunch("b", DateTimeOffset.UtcNow.AddDays(1), true),
            BuildLaunch("c", DateTimeOffset.UtcNow.AddDays(3), true),
            BuildLaunch("d", DateTimeOffset.UtcNow.AddDays(4), false)
        };

        var service = ServiceFactory.CreateService(launches, out var db);

        var result = await service.ImportUpcomingAsync(2, CancellationToken.None);

        Assert.Equal(4, result.FetchedFromApi);
        Assert.Equal(2, result.Processed);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(2, db.Launches.Count());
        Assert.DoesNotContain(db.Launches, x => x.ExternalId == "d");
    }

    [Fact]
    public async Task ImportUpcomingAsync_UpdatesExistingLaunch()
    {
        var existingId = "existing-id";
        var launches = new List<SpaceXLaunchDto>
        {
            BuildLaunch(existingId, DateTimeOffset.UtcNow.AddDays(5), true, missionName: "New Name")
        };

        var service = ServiceFactory.CreateService(launches, out var db);

        db.Launches.Add(new SpaceLaunch
        {
            ExternalId = existingId,
            MissionName = "Old Name",
            LaunchDateUtc = DateTimeOffset.UtcNow.AddDays(10),
            Status = "upcoming",
            Launchpad = "pad-1",
            HasWebcast = false,
            WatchScore = 10,
            ImportedAtUtc = DateTimeOffset.UtcNow.AddDays(-3)
        });
        await db.SaveChangesAsync();

        var result = await service.ImportUpcomingAsync(10, CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal("New Name", db.Launches.Single().MissionName);
    }

    [Fact]
    public async Task ImportUpcomingAsync_AssignsMaxWatchScoreForSoonLaunchWithWebcast()
    {
        var launches = new List<SpaceXLaunchDto>
        {
            BuildLaunch("score-max", DateTimeOffset.UtcNow.AddDays(2), true, webcast: "https://example.test/live")
        };

        var service = ServiceFactory.CreateService(launches, out var db);

        await service.ImportUpcomingAsync(10, CancellationToken.None);

        Assert.Equal(100, db.Launches.Single().WatchScore);
    }

    [Fact]
    public async Task ImportUpcomingAsync_AssignsLowerWatchScoreForFarLaunchWithoutWebcast()
    {
        var launches = new List<SpaceXLaunchDto>
        {
            BuildLaunch("score-low", DateTimeOffset.UtcNow.AddDays(120), true)
        };

        var service = ServiceFactory.CreateService(launches, out var db);

        await service.ImportUpcomingAsync(10, CancellationToken.None);

        Assert.Equal(40, db.Launches.Single().WatchScore);
    }

    [Fact]
    public async Task ImportUpcomingAsync_AddsMissionTypeBonusForCrewMissions()
    {
        var launchDate = DateTimeOffset.UtcNow.AddDays(14);
        var launches = new List<SpaceXLaunchDto>
        {
            BuildLaunch("crew", launchDate, true, missionName: "Crew-10"),
            BuildLaunch("generic", launchDate, true, missionName: "Generic Mission")
        };

        var service = ServiceFactory.CreateService(launches, out var db);

        await service.ImportUpcomingAsync(10, CancellationToken.None);

        var crewScore = db.Launches.Single(x => x.ExternalId == "crew").WatchScore;
        var genericScore = db.Launches.Single(x => x.ExternalId == "generic").WatchScore;

        Assert.Equal(15, crewScore - genericScore);
    }

    [Fact]
    public async Task ImportUpcomingAsync_PenalizesMissingLaunchpad()
    {
        var launchDate = DateTimeOffset.UtcNow.AddDays(14);
        var launches = new List<SpaceXLaunchDto>
        {
            BuildLaunch("no-pad", launchDate, true, launchpad: null),
            BuildLaunch("with-pad", launchDate, true, launchpad: "pad-a")
        };

        var service = ServiceFactory.CreateService(launches, out var db);

        await service.ImportUpcomingAsync(10, CancellationToken.None);

        var noPadScore = db.Launches.Single(x => x.ExternalId == "no-pad").WatchScore;
        var withPadScore = db.Launches.Single(x => x.ExternalId == "with-pad").WatchScore;

        Assert.Equal(5, withPadScore - noPadScore);
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsCorrectCountsAndTopLaunchpads()
    {
        var service = ServiceFactory.CreateService([], out var db);
        db.Launches.AddRange(
            CreateStoredLaunch("1", "pad-a", DateTimeOffset.UtcNow.AddDays(2), true, 80),
            CreateStoredLaunch("2", "pad-a", DateTimeOffset.UtcNow.AddDays(10), false, 60),
            CreateStoredLaunch("3", "pad-b", DateTimeOffset.UtcNow.AddDays(40), true, 70),
            CreateStoredLaunch("4", null, DateTimeOffset.UtcNow.AddDays(-2), false, 55));
        await db.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(4, dashboard.TotalStored);
        Assert.Equal(2, dashboard.WithWebcast);
        Assert.Equal(2, dashboard.LaunchesInNext30Days);
        Assert.NotNull(dashboard.NextLaunch);
        Assert.Equal("pad-a", dashboard.TopLaunchpads.First().Launchpad);
        Assert.Equal(2, dashboard.TopLaunchpads.First().Count);
    }

    [Fact]
    public async Task GetDashboardAsync_FallsBackToEarliestKnownLaunchWhenNoFutureLaunchExists()
    {
        var service = ServiceFactory.CreateService([], out var db);
        db.Launches.AddRange(
            CreateStoredLaunch("1", "pad-a", DateTimeOffset.UtcNow.AddDays(-20), true, 70, "Older"),
            CreateStoredLaunch("2", "pad-b", DateTimeOffset.UtcNow.AddDays(-5), true, 75, "Recent"));
        await db.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.NotNull(dashboard.NextLaunch);
        Assert.Equal("Older", dashboard.NextLaunch!.MissionName);
    }

    [Fact]
    public async Task GetRecommendationsAsync_PrioritizesFutureLaunchesBeforePast()
    {
        var service = ServiceFactory.CreateService([], out var db);
        db.Launches.AddRange(
            CreateStoredLaunch("future", "pad-a", DateTimeOffset.UtcNow.AddDays(5), true, 60, "Future"),
            CreateStoredLaunch("past-high", "pad-a", DateTimeOffset.UtcNow.AddDays(-1), true, 100, "Past High"));
        await db.SaveChangesAsync();

        var result = await service.GetRecommendationsAsync(2, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Future", result[0].MissionName);
    }

    [Fact]
    public async Task GetRecommendationsAsync_ReturnsOnlyRequestedTake()
    {
        var service = ServiceFactory.CreateService([], out var db);
        db.Launches.AddRange(
            CreateStoredLaunch("1", "pad-a", DateTimeOffset.UtcNow.AddDays(1), true, 90),
            CreateStoredLaunch("2", "pad-a", DateTimeOffset.UtcNow.AddDays(2), true, 88),
            CreateStoredLaunch("3", "pad-a", DateTimeOffset.UtcNow.AddDays(3), true, 86));
        await db.SaveChangesAsync();

        var result = await service.GetRecommendationsAsync(2, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetRecommendationsAsync_IgnoresLaunchesWithoutDate()
    {
        var service = ServiceFactory.CreateService([], out var db);
        db.Launches.AddRange(
            CreateStoredLaunch("1", "pad-a", null, true, 90, "NoDate"),
            CreateStoredLaunch("2", "pad-a", DateTimeOffset.UtcNow.AddDays(1), true, 70, "WithDate"));
        await db.SaveChangesAsync();

        var result = await service.GetRecommendationsAsync(5, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("WithDate", result[0].MissionName);
    }

    private static SpaceXLaunchDto BuildLaunch(
        string id,
        DateTimeOffset? dateUtc,
        bool upcoming,
        string? webcast = null,
        string? missionName = null,
        string? launchpad = "pad-a")
        => new()
        {
            Id = id,
            Name = missionName ?? $"mission-{id}",
            DateUtc = dateUtc,
            Upcoming = upcoming,
            Launchpad = launchpad,
            Links = new SpaceXLinksDto { Webcast = webcast }
        };

    private static SpaceLaunch CreateStoredLaunch(
        string id,
        string? launchpad,
        DateTimeOffset? dateUtc,
        bool hasWebcast,
        int watchScore,
        string? missionName = null)
        => new()
        {
            ExternalId = id,
            MissionName = missionName ?? $"stored-{id}",
            LaunchDateUtc = dateUtc,
            Status = "upcoming",
            Launchpad = launchpad,
            HasWebcast = hasWebcast,
            WebcastUrl = hasWebcast ? "https://example.test/live" : null,
            WatchScore = watchScore,
            ImportedAtUtc = DateTimeOffset.UtcNow
        };
}
