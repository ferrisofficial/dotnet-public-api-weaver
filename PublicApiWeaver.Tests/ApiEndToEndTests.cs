using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PublicApiWeaver.Contracts;
using PublicApiWeaver.Services;

namespace PublicApiWeaver.Tests;

public sealed class ApiEndToEndTests
{
    [Fact]
    public async Task FullFlow_ImportThenReadDashboardAndRecommendations_WorksEndToEnd()
    {
        var launches = new List<SpaceXLaunchDto>
        {
            new()
            {
                Id = "e2e-1",
                Name = "E2E Mission One",
                DateUtc = DateTimeOffset.UtcNow.AddDays(2),
                Upcoming = true,
                Launchpad = "pad-e2e-a",
                Links = new SpaceXLinksDto { Webcast = "https://example.test/e2e-1" }
            },
            new()
            {
                Id = "e2e-2",
                Name = "E2E Mission Two",
                DateUtc = DateTimeOffset.UtcNow.AddDays(5),
                Upcoming = true,
                Launchpad = "pad-e2e-b",
                Links = new SpaceXLinksDto { Webcast = null }
            },
            new()
            {
                Id = "e2e-3",
                Name = "E2E Mission Three",
                DateUtc = DateTimeOffset.UtcNow.AddDays(9),
                Upcoming = true,
                Launchpad = "pad-e2e-a",
                Links = new SpaceXLinksDto { Webcast = "https://example.test/e2e-3" }
            }
        };

        await using var factory = new WeaverWebApplicationFactory(launches);
        var client = factory.CreateClient();

        var importResponse = await client.PostAsync("/api/weaver/import?limit=10", null);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        var importBody = await importResponse.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.NotNull(importBody);
        Assert.Equal(3, importBody!.Processed);
        Assert.Equal(3, importBody.Inserted);

        var dashboardResponse = await client.GetAsync("/api/weaver/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DashboardDto>();
        Assert.NotNull(dashboard);
        Assert.Equal(3, dashboard!.TotalStored);
        Assert.Equal(2, dashboard.WithWebcast);
        Assert.NotNull(dashboard.NextLaunch);
        Assert.Equal("E2E Mission One", dashboard.NextLaunch!.MissionName);

        var recommendationsResponse = await client.GetAsync("/api/weaver/recommendations?take=2");
        Assert.Equal(HttpStatusCode.OK, recommendationsResponse.StatusCode);

        var recommendations = await recommendationsResponse.Content.ReadFromJsonAsync<List<RecommendationDto>>();
        Assert.NotNull(recommendations);
        Assert.Equal(2, recommendations!.Count);
        Assert.Equal("E2E Mission One", recommendations[0].MissionName);
    }

    private sealed class WeaverWebApplicationFactory(IReadOnlyList<SpaceXLaunchDto> launches) : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"weaver-e2e-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LaunchDb"] = $"Data Source={_dbPath}"
                });
            });

            builder.ConfigureServices(services =>
            {
                var existingClient = services.SingleOrDefault(d => d.ServiceType == typeof(ISpaceXClient));
                if (existingClient is not null)
                {
                    services.Remove(existingClient);
                }

                services.AddSingleton<ISpaceXClient>(new FixedClient(launches));
            });
        }

    }

    private sealed class FixedClient(IReadOnlyList<SpaceXLaunchDto> launches) : ISpaceXClient
    {
        public Task<IReadOnlyList<SpaceXLaunchDto>> GetUpcomingLaunchesAsync(CancellationToken cancellationToken)
            => Task.FromResult(launches);
    }
}
