using Microsoft.AspNetCore.Mvc.RazorPages;
using FerryTimes.Core;
using FerryTimes.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FerryTimes.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly AppDbContext _context;

    public Timetable? NextFerryFromTahiti { get; private set; }

    TimeZoneInfo tahitiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Tahiti");

    public IndexModel(ILogger<IndexModel> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task OnGetAsync()
    {
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tahitiTimeZone);
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        NextFerryFromTahiti = await _context.Timetables.FirstOrDefaultAsync();
    }
}
