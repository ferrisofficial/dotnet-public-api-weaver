using Microsoft.EntityFrameworkCore;
using PublicApiWeaver.Data;
using PublicApiWeaver.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LaunchDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LaunchDb")));

builder.Services.AddHttpClient<ISpaceXClient, SpaceXClient>(client =>
{
    client.BaseAddress = new Uri("https://api.spacexdata.com/v5/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddScoped<LaunchImportService>();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LaunchDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/weaver/import", async (
    LaunchImportService service,
    int? limit,
    CancellationToken cancellationToken) =>
{
    var safeLimit = Math.Clamp(limit ?? 20, 1, 100);
    var summary = await service.ImportUpcomingAsync(safeLimit, cancellationToken);
    return Results.Ok(summary);
})
.WithName("ImportUpcomingLaunches");

app.MapGet("/api/weaver/dashboard", async (
    LaunchImportService service,
    CancellationToken cancellationToken) =>
{
    var dashboard = await service.GetDashboardAsync(cancellationToken);
    return Results.Ok(dashboard);
})
.WithName("GetDashboard");

app.MapGet("/api/weaver/recommendations", async (
    LaunchImportService service,
    int? take,
    CancellationToken cancellationToken) =>
{
    var safeTake = Math.Clamp(take ?? 5, 1, 20);
    var recommendations = await service.GetRecommendationsAsync(safeTake, cancellationToken);
    return Results.Ok(recommendations);
})
.WithName("GetRecommendations");

app.Run();

public partial class Program;
