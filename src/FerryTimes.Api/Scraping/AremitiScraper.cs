using System.Globalization;
using FerryTimes.Core;
using Microsoft.Playwright;

namespace FerryTimes.Api.Scraping;

public class AremitiScraper : IFerryScraper
{
    private const string TimetableUrl = "https://www.aremitiexpress.com/en/home/";

    private record RouteConfig(string TableSelector, string Origin, string Destination);

    public async Task<IReadOnlyList<Timetable>> ScrapeAsync(CancellationToken ct)
    {
        var results = new List<Timetable>();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(TimetableUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("#startDate");

        var startDateElement = await page.QuerySelectorAsync("#startDate") ?? throw new InvalidOperationException("Start date element not found on the page.");
        string startDateStr = (await startDateElement.InnerTextAsync()).Trim();
        var startDate = DateTime.ParseExact(startDateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);

        // Make sure both tables are loaded
        await page.WaitForSelectorAsync("#horaires-table-tahiti-moo");
        await page.WaitForSelectorAsync("#horaires-table-moo-tahiti");

        // Define the routes once
        var routes = new[]
        {
            new RouteConfig("#horaires-table-tahiti-moo", "Tahiti", "Moorea"),
            new RouteConfig("#horaires-table-moo-tahiti", "Moorea", "Tahiti")
        };

        foreach (var route in routes)
        {
            var timetables = await ExtractTimetablesAsync(page, route, startDate);
            results.AddRange(timetables);
        }

        await browser.CloseAsync();

        return results;
    }

    private static async Task<IEnumerable<Timetable>> ExtractTimetablesAsync(IPage page, RouteConfig config, DateTime startDate)
    {
        var timetables = new List<Timetable>();
        var dayElements = await page.QuerySelectorAllAsync($"{config.TableSelector} .day-of-week");
        DateTime tripDate = startDate;

        foreach (var dayElement in dayElements)
        {
            var timeElements = await dayElement.QuerySelectorAllAsync(".trip-date");

            foreach (var timeElement in timeElements)
            {
                string timeText = (await timeElement.InnerTextAsync()).Trim();
                var timeOfDay = DateTime.ParseExact(timeText, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;

                timetables.Add(new Timetable
                {
                    Departure = tripDate.Add(timeOfDay),
                    Origin = config.Origin,
                    Destination = config.Destination,
                    Company = "Aremiti"
                });
            }

            tripDate = tripDate.AddDays(1);
        }

        return timetables;
    }
}
