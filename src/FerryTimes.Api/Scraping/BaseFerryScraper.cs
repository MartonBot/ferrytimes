using System.Globalization;
using FerryTimes.Core;
using Microsoft.Playwright;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace FerryTimes.Api.Scraping;

public abstract class BaseFerryScraper : IFerryScraper
{
    private readonly IConfiguration _configuration;

    protected abstract string TimetableUrl { get; }
    protected abstract string StartDateSelector { get; }
    protected abstract string[] TableSelectors { get; }
    protected abstract string DateFormat { get; }
    protected abstract string CompanyName { get; }
    protected abstract Task<IEnumerable<Timetable>> ExtractTimetablesAsync(IPage page, DateTime weekStartDate, CancellationToken ct);

    protected BaseFerryScraper(IConfiguration configuration)
    {
        _configuration = configuration;
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
            await NotifyFailureAsync(CompanyName, ex.Message, _configuration);
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

    public static async Task NotifyFailureAsync(string scraperName, string errorMessage, IConfiguration configuration)
    {
        var smtpSettings = configuration.GetSection("SmtpSettings");
        var smtpHost = smtpSettings["Host"] ?? throw new InvalidOperationException("SMTP Host is not configured.");
        var smtpPort = smtpSettings["Port"] ?? throw new InvalidOperationException("SMTP Port is not configured.");
        var smtpUsername = smtpSettings["Username"] ?? throw new InvalidOperationException("SMTP Username is not configured.");
        var smtpPassword = smtpSettings["Password"] ?? throw new InvalidOperationException("SMTP Password is not configured.");
        var smtpEnableSsl = smtpSettings["EnableSsl"] ?? throw new InvalidOperationException("SMTP EnableSsl is not configured.");
        var fromEmail = smtpSettings["FromEmail"] ?? throw new InvalidOperationException("From Email is not configured.");
        var toEmail = smtpSettings["ToEmail"] ?? throw new InvalidOperationException("To Email is not configured.");

        var smtpClient = new SmtpClient(smtpHost)
        {
            Port = int.Parse(smtpPort),
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            EnableSsl = bool.Parse(smtpEnableSsl),
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromEmail),
            Subject = $"[Scraper Alert] {scraperName} failed",
            Body = $"The scraper '{scraperName}' encountered an error:\n\n{errorMessage}\n\nTime: {DateTime.UtcNow}",
            IsBodyHtml = false,
        };
        mailMessage.To.Add(toEmail);

        await smtpClient.SendMailAsync(mailMessage);
    }

}