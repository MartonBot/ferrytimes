using System.Globalization;
using FerryTimes.Core;
using Microsoft.Playwright;

namespace FerryTimes.Api.Scraping;

public class VaearaiScraper : IFerryScraper
{
    private const string TimetableUrl = "https://www.vaearai.com/horaires/";
    private const string StartDateSelector = "#startDate";
    private const string TahitiToMooreaSelector = "#horaires-table-tahiti-moo";
    private const string MooreaToTahitiSelector = "#horaires-table-moo-tahiti";
    private const string CalendarButtonSelector = "#bt_show_calendar";
    private const string CalendarMonthSelector = "#datepicker .ui-datepicker-month";
    private const string CalendarYearSelector = "#datepicker .ui-datepicker-year";
    private const string CalendarNextSelector = "#datepicker .ui-datepicker-next";
    private const string CalendarPrevSelector = "#datepicker .ui-datepicker-prev";
    private const string DateFormat = "dd/MM/yyyy";
    private const string TimeFormat = "HH:mm";
    private const string CompanyName = "Vaearai";

    private record RouteConfig(string TableSelector, string Origin, string Destination);

    public async Task<IReadOnlyList<Timetable>> ScrapeAsync(CancellationToken ct, int weeks = 1)
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

        for (int week = 0; week < weeks; week++)
        {
            var weekStartDate = startDate.AddDays(7 * week);

            if (week > 0)
            {
                await GoToWeekAsync(page, weekStartDate);
            }

            var routes = new[]
            {
                new RouteConfig(TahitiToMooreaSelector, "Tahiti", "Moorea"),
                new RouteConfig(MooreaToTahitiSelector, "Moorea", "Tahiti")
            };

            foreach (var route in routes)
            {
                var timetables = await ExtractTimetablesAsync(page, route, weekStartDate);
                results.AddRange(timetables);
            }
        }

        await browser.CloseAsync();

        return results;
    }

    private async Task GoToWeekAsync(IPage page, DateTime weekStartDate)
    {
        // Open the calendar picker
        await page.ClickAsync(CalendarButtonSelector);
        await page.WaitForSelectorAsync("#datepicker .ui-datepicker-calendar");

        int calendarMonth = weekStartDate.Month - 1;
        int calendarYear = weekStartDate.Year;

        while (true)
        {
            var monthText = await page.InnerTextAsync(CalendarMonthSelector);
            var yearText = await page.InnerTextAsync(CalendarYearSelector);
            var currentMonth = DateTime.ParseExact(monthText, "MMMM", CultureInfo.GetCultureInfo("fr-FR")).Month - 1;
            var currentYear = int.Parse(yearText);

            if (currentMonth == calendarMonth && currentYear == calendarYear)
                break;

            if (currentYear < calendarYear || (currentYear == calendarYear && currentMonth < calendarMonth))
                await page.ClickAsync(CalendarNextSelector);
            else
                await page.ClickAsync(CalendarPrevSelector);

            await page.WaitForTimeoutAsync(200);
        }

        string daySelector = $"#datepicker td[data-month='{calendarMonth}'][data-year='{calendarYear}'] a[data-date='{weekStartDate.Day}']";
        await page.ClickAsync(daySelector);

        // Wait for tables to reload
        await page.WaitForSelectorAsync(TahitiToMooreaSelector);
        await page.WaitForSelectorAsync(MooreaToTahitiSelector);
    }

    private static async Task<IEnumerable<Timetable>> ExtractTimetablesAsync(IPage page, RouteConfig config, DateTime startDate)
    {
        var timetables = new List<Timetable>();
        DateTime tripDate = startDate;

        // Select the table by its id
        var table = page.Locator(config.TableSelector);

        // Get all rows
        var rows = table.Locator("tr");

        foreach (var rowElement in await rows.ElementHandlesAsync())
        {
            var cellElements = await rowElement.QuerySelectorAllAsync("td");
            foreach (var cell in cellElements)
            {
                string timeText = (await cell.InnerTextAsync()).Trim();
                // Try to parse as a time, skip if not a valid time
                if (DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
                {
                    timetables.Add(new Timetable
                    {
                        Departure = tripDate.Add(parsedTime.TimeOfDay),
                        Origin = config.Origin,
                        Destination = config.Destination,
                        Company = CompanyName
                    });
                }
            }

            // Only increment tripDate if this row represents a day (either with times or with a "no service" message)
            if (cellElements.Count > 0)
            {
                tripDate = tripDate.AddDays(1);
            }
        }

        return timetables;
    }
}
