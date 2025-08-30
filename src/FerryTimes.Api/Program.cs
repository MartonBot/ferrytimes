using FerryTimes.Api.Data;
using FerryTimes.Api.Scraping;
using FerryTimes.Api.Services;
using FerryTimes.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQLite DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=timetables.db"));

// DI: scrapers + background service
builder.Services.AddScoped<IFerryScraper, AremitiScraper>();
builder.Services.AddHostedService<TimetableScraperService>();

// Minimal APIs
var app = builder.Build();

// Health
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Next boat
app.MapGet("/api/timetables/next", async (AppDbContext db, string from = "Tahiti") =>
{
    DateTime utcNow = DateTime.UtcNow;
    TimeZoneInfo tahitiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Tahiti");
    DateTime now = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tahitiTimeZone);
    var next = await db.Timetables
        .Where(t => t.Origin == from && t.Departure > now)
        .OrderBy(t => t.Departure)
        .FirstOrDefaultAsync();
    return next is null ? Results.NotFound() : Results.Ok(next);
});

// Today’s boats
app.MapGet("/api/timetables/today", async (AppDbContext db, string from, string to) =>
{
    var today = DateTime.UtcNow.Date;
    var tomorrow = today.AddDays(1);
    var list = await db.Timetables
        .Where(t => t.Origin == from && t.Destination == to &&
                    t.Departure >= today && t.Departure < tomorrow)
        .OrderBy(t => t.Departure)
        .ToListAsync();
    return Results.Ok(list);
});

// Scrape now
app.MapPost("/api/scrape-now", async (
    IEnumerable<IFerryScraper> scrapers,
    AppDbContext db,
    CancellationToken ct) =>
{
    var results = new List<Timetable>();

    foreach (var scraper in scrapers)
    {
        var data = await scraper.ScrapeAsync(ct);
        results.AddRange(data);
    }

    // (Simple refresh logic: wipe today’s entries for these companies, then insert fresh)
    var today = DateTime.UtcNow.Date;
    var todays = db.Timetables.Where(t => t.Departure.Date == today);
    db.Timetables.RemoveRange(todays);
    await db.SaveChangesAsync(ct);

    await db.Timetables.AddRangeAsync(results, ct);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        Count = results.Count,
        Message = $"Scrape complete at {DateTime.UtcNow}"
    });
});

app.Run();
