using FerryTimes.Api.Data;
using FerryTimes.Core;
using Microsoft.EntityFrameworkCore;

namespace FerryTimes.Api.Services;

public class TimetableScraperService : BackgroundService
{
    private readonly ILogger<TimetableScraperService> _logger;
    private readonly IServiceProvider _sp;

    public TimetableScraperService(ILogger<TimetableScraperService> logger, IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup, then every 60 minutes
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scrapers = scope.ServiceProvider.GetServices<IFerryScraper>();

                var results = new List<Timetable>();
                foreach (var scraper in scrapers)
                {
                    var data = await scraper.ScrapeAsync(stoppingToken);
                    results.AddRange(data);
                }

                // Simple refresh strategy: wipe today's and re-insert (tweak later)
                var today = DateTime.UtcNow.Date;
                var todays = db.Timetables.Where(t => t.Departure.Date == today);
                db.Timetables.RemoveRange(todays);
                await db.SaveChangesAsync(stoppingToken);

                await db.Timetables.AddRangeAsync(results, stoppingToken);
                await db.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Scrape cycle complete: {Count} records", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scrape cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
        }
    }
}
