namespace PublicApiWeaver.Models;

public sealed class SpaceLaunch
{
    public int Id { get; set; }
    public required string ExternalId { get; set; }
    public required string MissionName { get; set; }
    public DateTimeOffset? LaunchDateUtc { get; set; }
    public required string Status { get; set; }
    public string? Launchpad { get; set; }
    public bool HasWebcast { get; set; }
    public string? WebcastUrl { get; set; }
    public int WatchScore { get; set; }
    public DateTimeOffset ImportedAtUtc { get; set; }
}