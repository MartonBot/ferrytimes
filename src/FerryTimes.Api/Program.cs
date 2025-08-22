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
app.MapGet("/api/timetables/next", async (AppDbContext db, string from, string to) =>
{
    var now = DateTime.UtcNow;
    var next = await db.Timetables
        .Where(t => t.Origin == from && t.Destination == to && t.DepartureUtc > now)
        .OrderBy(t => t.DepartureUtc)
        .FirstOrDefaultAsync();
    return next is null ? Results.NotFound() : Results.Ok(next);
});

// Today’s boats (UTC—adjust later for local TZ)
app.MapGet("/api/timetables/today", async (AppDbContext db, string from, string to) =>
{
    var today = DateTime.UtcNow.Date;
    var tomorrow = today.AddDays(1);
    var list = await db.Timetables
        .Where(t => t.Origin == from && t.Destination == to &&
                    t.DepartureUtc >= today && t.DepartureUtc < tomorrow)
        .OrderBy(t => t.DepartureUtc)
        .ToListAsync();
    return Results.Ok(list);
});

app.Run();
