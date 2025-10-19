using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace FerryTimes.Core.Scraping;

public class VaearaiScraper(FailureNotifier failureNotifier, ILogger<VaearaiScraper> logger)
    : BaseFerryScraper(failureNotifier, logger)
{
    protected override string TimetableUrl => "https://www.vaearai.com/horaires/";
    protected override string StartDateSelector => "#startDate";
    protected override string[] TableSelectors => ["#horaires-table-tahiti-moo", "#horaires-table-moo-tahiti"];
    protected override string DateFormat => "dd/MM/yyyy";
    protected override string CompanyName => "Vaearai";

    private const string TimeFormat = "HH:mm";

    protected override async Task<IEnumerable<Timetable>> ExtractTimetablesAsync(IPage page, DateTime weekStartDate, CancellationToken ct)
    {
        var timetables = new List<Timetable>();
        var routes = new[]
        {
            new { TableSelector = "#horaires-table-tahiti-moo", Origin = "Tahiti", Destination = "Moorea" },
            new { TableSelector = "#horaires-table-moo-tahiti", Origin = "Moorea", Destination = "Tahiti" }
        };

        foreach (var route in routes)
        {
            var table = page.Locator(route.TableSelector);
            var rows = table.Locator("tr");
            DateTime tripDate = weekStartDate;

            foreach (var rowElement in await rows.ElementHandlesAsync())
            {
                var cellElements = await rowElement.QuerySelectorAllAsync("td");
                foreach (var cell in cellElements)
                {
                    string timeText = (await cell.InnerTextAsync()).Trim();
                    if (DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
                    {
                        timetables.Add(new Timetable
                        {
                            Departure = tripDate.Add(parsedTime.TimeOfDay),
                            Origin = route.Origin,
                            Destination = route.Destination,
                            Company = CompanyName
                        });
                    }
                }
                if (cellElements.Count > 0)
                    tripDate = tripDate.AddDays(1);
            }
        }
        return timetables;
    }
}
