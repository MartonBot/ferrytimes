namespace FerryTimes.Core;

public class Timetable
{
    public int Id { get; set; }
    public DateTime Departure { get; set; }
    public DateTime Arrival { get; set; }
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Company { get; set; } = "";
}

public interface IFerryScraper
{
    Task<IReadOnlyList<Timetable>> ScrapeAsync(CancellationToken ct, int weeks = 1);
}
