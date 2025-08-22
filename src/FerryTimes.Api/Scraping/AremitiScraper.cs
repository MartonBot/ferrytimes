using FerryTimes.Core;
using Microsoft.Playwright;

namespace FerryTimes.Api.Scraping;

public class AremitiScraper : IFerryScraper
{
    private const string TimetableUrl = "https://www.aremitiexpress.com/en/home/";

    public async Task<IReadOnlyList<Timetable>> ScrapeAsync(CancellationToken ct)
    {
        var results = new List<Timetable>();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(TimetableUrl);

        // Wait for timetable table to load (update selector if necessary)
        await page.WaitForSelectorAsync("#horaires-table-tahiti-moo");

        // Dictionary to hold the schedule
        var schedule = new Dictionary<string, List<string>>();

        // Select all days
        var dayElements = await page.QuerySelectorAllAsync("#horaires-table-tahiti-moo .day-of-week");

        foreach (var dayElement in dayElements)
        {
            // Get the day name
            var headerElement = await dayElement.QuerySelectorAsync(".header");
            string dayName = (await headerElement.InnerTextAsync()).Trim();

            // Get all times
            var timeElements = await dayElement.QuerySelectorAllAsync(".trip-date");
            var times = new List<string>();
            foreach (var timeElement in timeElements)
            {
                string timeText = (await timeElement.InnerTextAsync()).Trim();
                times.Add(timeText);
            }

            schedule[dayName] = times;
        }

        // Print the schedule
        foreach (var day in schedule)
        {
            Console.WriteLine($"{day.Key}: {string.Join(", ", day.Value)}");
        }

        await browser.CloseAsync();

        return results;
    }
}
