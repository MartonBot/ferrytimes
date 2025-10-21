using FerryTimes.Core;
using FerryTimes.Core.Data;
using FerryTimes.Core.Scraping;
using FerryTimes.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add FerryTimes.Core services
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=../../data/timetables.db"));

builder.Services.AddScoped<IFerryScraper, TerevauScraper>();
builder.Services.AddScoped<IFerryScraper, AremitiScraper>();
builder.Services.AddScoped<IFerryScraper, VaearaiScraper>();

if (builder.Configuration.GetValue<bool>("Features:EnableTimetableScraping"))
{
    builder.Services.AddHostedService<TimetableScraperService>();
}


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Add some basic request logging
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"Request {context.Request.Method} {context.Request.Path}");
    await next();
});

app.MapRazorPages();

app.Run();
