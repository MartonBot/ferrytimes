using FerryTimes.Api.Data;
using FerryTimes.Core;

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
                    _logger.LogInformation("Scraped {Count} records for {ScraperName}", data.Count, scraper.GetType().Name);
                    results.AddRange(data);
                }

                // Simple refresh strategy: wipe everything and re-insert (tweak later)
                db.Timetables.RemoveRange(db.Timetables);
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
