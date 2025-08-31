using FerryTimes.Api.Data;
using FerryTimes.Api.Scraping;
using FerryTimes.Api.Services;
using FerryTimes.Core;
using Microsoft.EntityFrameworkCore;

TimeZoneInfo tahitiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Tahiti");

var builder = WebApplication.CreateBuilder(args);

// SQLite DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=timetables.db"));

// DI: scrapers + background service
builder.Services.AddScoped<IFerryScraper, AremitiScraper>();
builder.Services.AddScoped<IFerryScraper, VaearaiScraper>();
builder.Services.AddHostedService<TimetableScraperService>();

// Minimal APIs
var app = builder.Build();

// Health
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Next boat
app.MapGet("/api/timetables/next", async (AppDbContext db, string from = "Tahiti") =>
{
    DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tahitiTimeZone);
    var next = await db.Timetables
        .Where(t => t.Origin == from && t.Departure > now)
        .OrderBy(t => t.Departure)
        .Select(t => new
        {
            Departure = t.Departure.ToString("dd/MM HH:mm"),
            t.Origin,
            t.Company
        })
        .FirstOrDefaultAsync();
    return next is null ? Results.NotFound() : Results.Ok(next);
});

// Today’s boats
app.MapGet("/api/timetables/today", async (AppDbContext db, string from = "") =>
{
    DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tahitiTimeZone);
    var today = now.Date;
    var tomorrow = today.AddDays(1);
    var list = await db.Timetables
        .Where(t => (string.IsNullOrWhiteSpace(from) || t.Origin == from) &&
                    t.Departure >= today && t.Departure < tomorrow)
        .OrderBy(t => t.Departure)
        .Select(t => new
        {
            Departure = t.Departure.ToString("dd/MM HH:mm"),
            t.Origin,
            t.Company
        })
        .ToListAsync();
    return Results.Ok(list);
});

// Today’s boats by company
app.MapGet("/api/timetables/today/{companyname}", async (AppDbContext db, string companyname, string from = "") =>
{
    DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tahitiTimeZone);
    var today = now.Date;
    var tomorrow = today.AddDays(1);
    var list = await db.Timetables
        .Where(t =>
            t.Company.ToLower() == companyname.ToLower() &&
            (string.IsNullOrWhiteSpace(from) || t.Origin == from) &&
            t.Departure >= today && t.Departure < tomorrow)
        .OrderBy(t => t.Departure)
        .Select(t => new
        {
            Departure = t.Departure.ToString("dd/MM HH:mm"),
            t.Origin,
            t.Company
        })
        .ToListAsync();
    return Results.Ok(list);
});

// Scrape now the current week and the following
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

    // todo make sure we have scraped successfully for each company - otherwise we don't want to delete the old data

    // (Simple refresh logic: wipe all entries, then insert fresh)
    db.Timetables.RemoveRange(db.Timetables);
    await db.SaveChangesAsync(ct);

    await db.Timetables.AddRangeAsync(results, ct);
    await db.SaveChangesAsync(ct);

    DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tahitiTimeZone);

    return Results.Ok(new
    {
        Count = results.Count,
        Message = $"Scraped {results.Count} records at {now}"
    });
});

app.Run();
