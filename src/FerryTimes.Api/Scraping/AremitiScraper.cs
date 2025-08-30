using System.Globalization;
using System.Text.Json;
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

        await page.WaitForSelectorAsync("#startDate");

        string startDateStr = (await (await page.QuerySelectorAsync("#startDate")).InnerTextAsync()).Trim();
        var startDate = DateTime.ParseExact(startDateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        DateTime tripDate = startDate;

        // Wait for timetable table to load (update selector if necessary)
        await page.WaitForSelectorAsync("#horaires-table-tahiti-moo");
        await page.WaitForSelectorAsync("#horaires-table-moo-tahiti");

        // Dictionary to hold the schedule
        var schedule = new Dictionary<string, List<string>>();

        // Select all days
        var dayElementsFromTahiti = await page.QuerySelectorAllAsync("#horaires-table-tahiti-moo .day-of-week");

        foreach (var dayElement in dayElementsFromTahiti)
        {
            // Get the day name
            var headerElement = await dayElement.QuerySelectorAsync(".header");

            // Get all times
            var timeElements = await dayElement.QuerySelectorAllAsync(".trip-date");
            var times = new List<string>();
            foreach (var timeElement in timeElements)
            {
                string timeText = (await timeElement.InnerTextAsync()).Trim();
                var timeOfDay = DateTime.ParseExact(timeText, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
                Timetable timetable = new()
                {
                    Departure = tripDate.Add(timeOfDay),
                    Origin = "Tahiti",
                    Destination = "Moorea",
                    Company = "Aremiti"
                };
                results.Add(timetable);
            }

            tripDate = tripDate.AddDays(1);
        }

        await browser.CloseAsync();

        return results;
    }
}
