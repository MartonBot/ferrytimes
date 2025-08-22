using FerryTimes.Core;

namespace FerryTimes.Api.Scraping;

public class AremitiScraper : IFerryScraper
{
    public Task<IReadOnlyList<Timetable>> ScrapeAsync(CancellationToken ct)
    {
        // TODO: implement Playwright scraping here
        IReadOnlyList<Timetable> demo = Array.Empty<Timetable>();
        return Task.FromResult(demo);
    }
}
