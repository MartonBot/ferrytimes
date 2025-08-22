using FerryTimes.Core;
using Microsoft.EntityFrameworkCore;

namespace FerryTimes.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Timetable> Timetables => Set<Timetable>();
}
