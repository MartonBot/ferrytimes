using System.Globalization;
using FerryTimes.Core;
using Microsoft.Playwright;

namespace FerryTimes.Api.Scraping;

public class AremitiScraper(IConfiguration configuration) : BaseFerryScraper(configuration)
{
    protected override string TimetableUrl => "https://www.aremitiexpress.com/en/home/";
    protected override string StartDateSelector => "#startDate";
    protected override string[] TableSelectors => ["#horaires-table-tahiti-moo", "#horaires-table-moo-tahiti"];
    protected override string DateFormat => "dd/MM/yyyy";
    protected override string CompanyName => "Aremiti";

    private const string DayOfWeekSelector = ".day-of-week";
    private const string TripDateSelector = ".trip-date";
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
            var dayElements = await page.QuerySelectorAllAsync($"{route.TableSelector} {DayOfWeekSelector}");
            DateTime tripDate = weekStartDate;

            foreach (var dayElement in dayElements)
            {
                var timeElements = await dayElement.QuerySelectorAllAsync(TripDateSelector);

                foreach (var timeElement in timeElements)
                {
                    string timeText = (await timeElement.InnerTextAsync()).Trim();
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
                tripDate = tripDate.AddDays(1);
            }
        }
        return timetables;
    }
}
