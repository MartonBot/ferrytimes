using Microsoft.EntityFrameworkCore;

namespace FerryTimes.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Timetable> Timetables => Set<Timetable>();
}
