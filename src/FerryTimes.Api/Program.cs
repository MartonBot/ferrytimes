using FerryTimes.Core.Data;
using FerryTimes.Core.Scraping;
using FerryTimes.Core.Services;
using FerryTimes.Core;
using Microsoft.EntityFrameworkCore;
using Serilog;

TimeZoneInfo tahitiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Tahiti");

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine("logs", "app-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        )
    );

// SQLite DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=timetables.db"));

// DI: scrapers + background service
builder.Services.AddScoped<IFerryScraper, TerevauScraper>();
builder.Services.AddScoped<IFerryScraper, AremitiScraper>();
builder.Services.AddScoped<IFerryScraper, VaearaiScraper>();
builder.Services.AddHostedService<TimetableScraperService>();
builder.Services.AddHostedService<ApiUsageLogProcessor>();
builder.Services.AddSingleton<FailureNotifier>();

// Minimal APIs
var app = builder.Build();

// Middleware to log API usage separately
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        Log.ForContext("ApiUsage", true)
           .Information("API Hit {Endpoint} {Params} at {TimeUtc}",
                context.Request.Path,
                context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "",
                DateTime.UtcNow);
    }

    await next();
});

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

// Timetables for a specified day
app.MapGet("/api/timetables/day", async (AppDbContext db, string date, string from = "") =>
{
    if (!DateTime.TryParse(date, out var targetDate))
    {
        return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });
    }

    DateTime dayStart = targetDate.Date;
    DateTime dayEnd = dayStart.AddDays(1);

    var list = await db.Timetables
        .Where(t => (string.IsNullOrWhiteSpace(from) || t.Origin == from) &&
                    t.Departure >= dayStart && t.Departure < dayEnd)
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
