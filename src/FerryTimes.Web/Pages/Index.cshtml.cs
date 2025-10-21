using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FerryTimes.Core;
using FerryTimes.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FerryTimes.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly AppDbContext _context;

    public List<Timetable> FilteredTimetables { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string> From { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string> Company { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public DateTime? Date { get; set; }

    TimeZoneInfo tahitiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Tahiti");

    public IndexModel(ILogger<IndexModel> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task OnGetAsync()
    {
        await LoadFilteredTimetables();
    }

    public async Task<IActionResult> OnGetFilteredTimetablesAsync()
    {
        await LoadFilteredTimetables();
        return new JsonResult(FilteredTimetables.Select(t => new
        {
            departure = t.Departure.ToString("ddd dd/MM HH:mm"),
            origin = t.Origin,
            company = t.Company
        }));
    }

    private async Task LoadFilteredTimetables()
    {
        // Set default values if no filters are selected
        if (!From.Any())
        {
            From.Add("Tahiti");
        }
        if (!Company.Any())
        {
            Company.AddRange(new[] { "Aremiti", "Terevau", "Vaearai", "Tauati" });
        }

        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tahitiTimeZone);
        DateTime filterDate = Date?.Date ?? now.Date;
        DateTime startOfDay = filterDate;
        DateTime endOfDay = filterDate.AddDays(1);

        var query = _context.Timetables
            .Where(t => From.Contains(t.Origin))
            .Where(t => Company.Contains(t.Company))
            .Where(t => t.Departure >= startOfDay && t.Departure < endOfDay);

        // If we're filtering for today, only show future departures
        if (filterDate.Date == now.Date)
        {
            query = query.Where(t => t.Departure >= now);
        }

        FilteredTimetables = await query
            .OrderBy(t => t.Departure)
            .Take(25)
            .ToListAsync();
    }
}
