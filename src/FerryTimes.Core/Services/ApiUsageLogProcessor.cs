using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace FerryTimes.Core.Services;

public class ApiUsageLogProcessor : BackgroundService
{
    private readonly ILogger<ApiUsageLogProcessor> _logger;
    private readonly string _logsDir = "logs";
    private readonly string _connectionString = "Data Source=stats.db";

    public ApiUsageLogProcessor(ILogger<ApiUsageLogProcessor> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLogsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing API usage logs");
            }

            // Run every 24 hours
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessLogsAsync()
    {
        Directory.CreateDirectory(_logsDir);

        var today = DateTime.UtcNow.Date;
        var logFiles = Directory.GetFiles(_logsDir, "api-usage-*.log")
            .Where(f => File.GetCreationTimeUtc(f).Date < today) // skip today's file
            .ToList();

        foreach (var file in logFiles)
        {
            _logger.LogInformation("Processing log file {File}", file);
            var lines = await File.ReadAllLinesAsync(file);

            var stats = new Dictionary<string, int>();

            foreach (var line in lines)
            {
                // Example line: API Hit /api/timetables/next ?from=IslandA&to=IslandB at 2025-09-04T10:22:34Z
                var match = Regex.Match(line, @"API Hit (?<endpoint>\S+)\s(?<params>\S*)");
                if (match.Success)
                {
                    var endpoint = match.Groups["endpoint"].Value;
                    var query = match.Groups["params"].Value ?? "";

                    var key = $"{endpoint} {query}";
                    stats[key] = stats.GetValueOrDefault(key, 0) + 1;
                }
            }

            await SaveStatsAsync(stats, file);

            // Optionally archive log file after processing
            var archivePath = Path.Combine(_logsDir, "archive", Path.GetFileName(file));
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
            File.Move(file, archivePath, overwrite: true);
        }
    }

    private async Task SaveStatsAsync(Dictionary<string, int> stats, string file)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ApiUsageStats (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL,
                Endpoint TEXT NOT NULL,
                Params TEXT,
                Count INTEGER NOT NULL
            );";
        await cmd.ExecuteNonQueryAsync();

        foreach (var kv in stats)
        {
            var parts = kv.Key.Split(' ', 2);
            var endpoint = parts[0];
            var query = parts.Length > 1 ? parts[1] : "";

            var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO ApiUsageStats (Date, Endpoint, Params, Count) VALUES ($date, $endpoint, $params, $count)";
            insert.Parameters.AddWithValue("$date", Path.GetFileNameWithoutExtension(file).Replace("api-usage-", ""));
            insert.Parameters.AddWithValue("$endpoint", endpoint);
            insert.Parameters.AddWithValue("$params", query);
            insert.Parameters.AddWithValue("$count", kv.Value);
            await insert.ExecuteNonQueryAsync();
        }
    }
}
