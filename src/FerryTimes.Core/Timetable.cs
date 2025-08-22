namespace FerryTimes.Core;

public class Timetable
{
    public int Id { get; set; }
    public DateTime DepartureUtc { get; set; }
    public DateTime ArrivalUtc { get; set; }
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Company { get; set; } = "";
}

public interface IFerryScraper
{
    Task<IReadOnlyList<Timetable>> ScrapeAsync(CancellationToken ct);
}
