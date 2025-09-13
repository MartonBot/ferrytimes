using System.Globalization;
using FerryTimes.Core;
using Microsoft.Playwright;
namespace FerryTimes.Api.Scraping;

public abstract class BaseFerryScraper : IFerryScraper
{
    protected abstract string TimetableUrl { get; }
    protected abstract string StartDateSelector { get; }
    protected abstract string[] TableSelectors { get; }
    protected abstract string DateFormat { get; }
    protected abstract string CompanyName { get; }
    protected abstract Task<IEnumerable<Timetable>> ExtractTimetablesAsync(IPage page, DateTime weekStartDate, CancellationToken ct);
    private readonly FailureNotifier _failureNotifier;

    protected BaseFerryScraper(FailureNotifier failureNotifier)
    {
        _failureNotifier = failureNotifier;
    }

    public async Task<IReadOnlyList<Timetable>> ScrapeAsync(CancellationToken ct, int weeks = 1)
    {
        var results = new List<Timetable>();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

            var page = await browser.NewPageAsync();
            await page.GotoAsync(TimetableUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForSelectorAsync(StartDateSelector);

            var startDateElement = await page.QuerySelectorAsync(StartDateSelector) ?? throw new InvalidOperationException("Start date element not found on the page.");
            string startDateStr = (await startDateElement.InnerTextAsync()).Trim();
            var startDate = DateTime.ParseExact(startDateStr, DateFormat, CultureInfo.InvariantCulture);

            // Wait for all required tables
            foreach (var selector in TableSelectors)
                await page.WaitForSelectorAsync(selector);

            for (int week = 0; week < weeks; week++)
            {
                var weekStartDate = startDate.AddDays(7 * week);

                if (week > 0)
                    await GoToWeekAsync(page, weekStartDate);

                var weekTimetables = await ExtractTimetablesAsync(page, weekStartDate, ct);
                results.AddRange(weekTimetables);
            }

            await browser.CloseAsync();
        }
        catch (Exception ex)
        {
            await _failureNotifier.NotifyFailureAsync(CompanyName, ex.Message);
        }

        return results;
    }

    protected virtual async Task GoToWeekAsync(IPage page, DateTime weekStartDate)
    {
        // Default implementation for calendar navigation (override if needed)
        await page.ClickAsync("#bt_show_calendar");
        await page.WaitForSelectorAsync("#datepicker .ui-datepicker-calendar");

        int calendarMonth = weekStartDate.Month - 1;
        int calendarYear = weekStartDate.Year;

        while (true)
        {
            var monthText = await page.InnerTextAsync("#datepicker .ui-datepicker-month");
            var yearText = await page.InnerTextAsync("#datepicker .ui-datepicker-year");
            var currentMonth = DateTime.ParseExact(monthText, "MMMM", CultureInfo.GetCultureInfo("fr-FR")).Month - 1;
            var currentYear = int.Parse(yearText);

            if (currentMonth == calendarMonth && currentYear == calendarYear)
                break;

            if (currentYear < calendarYear || (currentYear == calendarYear && currentMonth < calendarMonth))
                await page.ClickAsync("#datepicker .ui-datepicker-next");
            else
                await page.ClickAsync("#datepicker .ui-datepicker-prev");

            await page.WaitForTimeoutAsync(200);
        }

        string daySelector = $"#datepicker td[data-month='{calendarMonth}'][data-year='{calendarYear}'] a";
        await page.ClickAsync(daySelector);

        // Wait for all required tables
        foreach (var selector in TableSelectors)
            await page.WaitForSelectorAsync(selector);
    }
}