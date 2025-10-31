// Services/MaintenanceNotifier.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // <-- este falta
using System.Globalization;
using Organizacional.Data;
using Microsoft.EntityFrameworkCore;
using Organizacional.Services;

public class MaintenanceNotifier : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MaintenanceNotificationsOptions> _opt;
    private readonly ILogger<MaintenanceNotifier> _logger;

    public MaintenanceNotifier(IServiceScopeFactory scopeFactory,
                               IOptions<MaintenanceNotificationsOptions> opt,
                               ILogger<MaintenanceNotifier> logger)
    {
        _scopeFactory = scopeFactory;
        _opt = opt;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = _opt.Value;

        if (cfg.UseDailySchedule)
        {
            // === Programa a una hora exacta cada día ===
            var tz = SafeFindTimeZone(cfg.TimeZone);
            while (!stoppingToken.IsCancellationRequested)
            {
                var nowUtc = DateTime.UtcNow;
                var nowTz  = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

                // siguiente ocurrencia de DailyTime en la zona horaria indicada
                TimeOnly.TryParseExact(cfg.DailyTime, "HH:mm", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var daily);
                var nextTz = nowTz.Date.Add(daily.ToTimeSpan());
                if (nowTz >= nextTz) nextTz = nextTz.AddDays(1);

                var delay = nextTz - nowTz;
                _logger.LogInformation("MaintenanceNotifier: próxima ejecución local {NextLocal} ({TZ})",
                    nextTz, tz.Id);

                await Task.Delay(delay, stoppingToken);
                await RunOnce(stoppingToken, tz);
            }
        }
        else
        {
            // === Modo periódico (el que ya tenías) ===
            if (cfg.RunImmediatelyOnStartup)
            {
                var tz = SafeFindTimeZone(cfg.TimeZone);
                await RunOnce(stoppingToken, tz);
            }

            var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, cfg.CheckIntervalMinutes)));
            var tz2   = SafeFindTimeZone(cfg.TimeZone);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnce(stoppingToken, tz2);
            }
        }
    }

    private async Task RunOnce(CancellationToken ct, TimeZoneInfo tz)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db   = scope.ServiceProvider.GetRequiredService<OrganizacionalContext>();
            var mail = scope.ServiceProvider.GetRequiredService<EmailService>();
            var job  = scope.ServiceProvider.GetRequiredService<MaintenanceNotificationJob>();
            var cfg  = scope.ServiceProvider.GetRequiredService<IOptions<MaintenanceNotificationsOptions>>().Value;

            await job.ExecuteOnceAsync(db, mail, cfg, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MaintenanceNotifier.RunOnce");
        }
    }

    private static TimeZoneInfo SafeFindTimeZone(string? id)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch { /* cae a Local */ }
        return TimeZoneInfo.Local;
    }
}