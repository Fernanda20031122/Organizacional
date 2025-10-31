using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

public class ExecutionDateNotifier : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ExecutionNotificationsOptions> _opt;
    private readonly ILogger<ExecutionDateNotifier> _logger;
    private readonly ExecutionDateNotificationJob _job;

    public ExecutionDateNotifier(
        IServiceScopeFactory scopeFactory,
        IOptions<ExecutionNotificationsOptions> opt,
        ILogger<ExecutionDateNotifier> logger,
        ExecutionDateNotificationJob job)
    {
        _scopeFactory = scopeFactory;
        _opt = opt;
        _logger = logger;
        _job = job;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = _opt.Value;
        var tz = TimeZoneInfo.FindSystemTimeZoneById(cfg.TimeZone);

        if (cfg.RunImmediatelyOnStartup)
        {
            _logger.LogInformation("ExecutionDateNotifier: primera corrida inmediata.");
            await _job.RunOnceAsync(stoppingToken, tz);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!cfg.UseDailySchedule)
            {
                // fallback: intervalo en minutos
                await Task.Delay(TimeSpan.FromMinutes(cfg.CheckIntervalMinutes), stoppingToken);
                await _job.RunOnceAsync(stoppingToken, tz);
            }
            else
            {
                // Programación diaria fija HH:mm
                TimeOnly.TryParseExact(cfg.DailyTime, "HH:mm", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var daily);

                var nowTz = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
                var nextTz = nowTz.Date.Add(daily.ToTimeSpan());
                if (nowTz >= nextTz) nextTz = nextTz.AddDays(1);

                var delay = nextTz - nowTz;
                _logger.LogInformation("ExecutionDateNotifier: próxima ejecución local {NextLocal} ({TZ})",
                    nextTz, tz.Id);

                await Task.Delay(delay, stoppingToken);
                await _job.RunOnceAsync(stoppingToken, tz);
            }
        }
    }
}
