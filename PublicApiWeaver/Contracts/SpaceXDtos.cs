using System.Text.Json.Serialization;

namespace PublicApiWeaver.Contracts;

public sealed class SpaceXLaunchDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("date_utc")]
    public DateTimeOffset? DateUtc { get; init; }

    [JsonPropertyName("upcoming")]
    public bool Upcoming { get; init; }

    [JsonPropertyName("launchpad")]
    public string? Launchpad { get; init; }

    [JsonPropertyName("links")]
    public SpaceXLinksDto? Links { get; init; }
}

public sealed class SpaceXLinksDto
{
    [JsonPropertyName("webcast")]
    public string? Webcast { get; init; }
}