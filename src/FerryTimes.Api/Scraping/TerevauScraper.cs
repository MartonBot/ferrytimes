using System.Globalization;
using FerryTimes.Core;
using Microsoft.Playwright;

namespace FerryTimes.Api.Scraping;

public class TerevauScraper(IConfiguration configuration) : BaseFerryScraper(configuration)
{
    protected override string TimetableUrl => "https://www.terevau.pf/horaires/";
    protected override string StartDateSelector => "#startDate";
    protected override string[] TableSelectors => ["#horaires-table-tahiti-moo", "#horaires-table-moo-tahiti"];
    protected override string DateFormat => "dd/MM/yyyy";
    protected override string CompanyName => "Terevau";

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
            var dayRows = await page.QuerySelectorAllAsync($"{route.TableSelector} tbody tr");
            DateTime tripDate = weekStartDate;

            foreach (var dayRow in dayRows)
            {
                var timeCells = await dayRow.QuerySelectorAllAsync("td");

                foreach (var timeCell in timeCells)
                {
                    string timeText = (await timeCell.InnerTextAsync()).Trim();
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