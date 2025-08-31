using System.Globalization;
using FerryTimes.Core;
using Microsoft.Playwright;

namespace FerryTimes.Api.Scraping;

public class AremitiScraper : IFerryScraper
{
    private const string TimetableUrl = "https://www.aremitiexpress.com/en/home/";
    private const string StartDateSelector = "#startDate";
    private const string TahitiToMooreaSelector = "#horaires-table-tahiti-moo";
    private const string MooreaToTahitiSelector = "#horaires-table-moo-tahiti";
    private const string DayOfWeekSelector = ".day-of-week";
    private const string TripDateSelector = ".trip-date";
    private const string DateFormat = "dd/MM/yyyy";
    private const string TimeFormat = "HH:mm";
    private const string CompanyName = "Aremiti";

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
        await page.WaitForSelectorAsync(StartDateSelector);

        var startDateElement = await page.QuerySelectorAsync(StartDateSelector) ?? throw new InvalidOperationException("Start date element not found on the page.");
        string startDateStr = (await startDateElement.InnerTextAsync()).Trim();
        var startDate = DateTime.ParseExact(startDateStr, DateFormat, CultureInfo.InvariantCulture);

        // Make sure both tables are loaded
        await page.WaitForSelectorAsync(TahitiToMooreaSelector);
        await page.WaitForSelectorAsync(MooreaToTahitiSelector);

        // Define the routes once
        var routes = new[]
        {
            new RouteConfig(TahitiToMooreaSelector, "Tahiti", "Moorea"),
            new RouteConfig(MooreaToTahitiSelector, "Moorea", "Tahiti")
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
        var dayElements = await page.QuerySelectorAllAsync($"{config.TableSelector} {DayOfWeekSelector}");
        DateTime tripDate = startDate;

        foreach (var dayElement in dayElements)
        {
            var timeElements = await dayElement.QuerySelectorAllAsync(TripDateSelector);

            foreach (var timeElement in timeElements)
            {
                string timeText = (await timeElement.InnerTextAsync()).Trim();
                var timeOfDay = DateTime.ParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture).TimeOfDay;

                timetables.Add(new Timetable
                {
                    Departure = tripDate.Add(timeOfDay),
                    Origin = config.Origin,
                    Destination = config.Destination,
                    Company = CompanyName
                });
            }

            tripDate = tripDate.AddDays(1);
        }

        return timetables;
    }
}
