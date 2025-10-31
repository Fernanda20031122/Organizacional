using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Services;
using System.Globalization;

public class ExecutionNotificationsOptions
{
    public int LeadDays { get; set; } = 7;
    public int[]? LeadDaysSet { get; set; } = new[] { 7, 3, 1 };

    // Modo periódico (fallback)
    public int CheckIntervalMinutes { get; set; } = 60;
    public bool RunImmediatelyOnStartup { get; set; } = false;

    // Modo diario fijo
    public bool UseDailySchedule { get; set; } = true;
    public string DailyTime { get; set; } = "07:30";          // HH:mm
    public string TimeZone { get; set; } = "America/Bogota";  // IANA

    // Envío
    public bool IncludeAdminsAlways { get; set; } = true;     // admins (rol 1)
    public bool IncludeTechs { get; set; } = true;            // técnicos asignados (rol 2)

    public string? BaseUrl { get; set; }

    // Redirección opcional en dev (para pruebas)
    public string? DevOverrideTo { get; set; }
}

public class ExecutionDateNotificationJob
{
    private readonly ILogger<ExecutionDateNotificationJob> _logger;
    private readonly EmailService _email;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ExecutionNotificationsOptions> _options;

    public ExecutionDateNotificationJob(
        ILogger<ExecutionDateNotificationJob> logger,
        EmailService email,
        IServiceScopeFactory scopeFactory,
        IOptions<ExecutionNotificationsOptions> options)
    {
        _logger = logger;
        _email = email;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    /// <summary>
    /// Revisa documentos cuya FechaEjecucion caiga exactamente en targetDay (fecha local) y notifica.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken ct, TimeZoneInfo tz)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizacionalContext>();
        var cfg = _options.Value;

        // Set de días objetivo (si no viene configurado, usamos LeadDays)
        var leadSet = (cfg.LeadDaysSet != null && cfg.LeadDaysSet.Length > 0)
            ? cfg.LeadDaysSet.Distinct().OrderByDescending(x => x).ToArray()   // p.ej. [7,3,1]
            : new[] { cfg.LeadDays };

        var nowTz = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        var baseUrl = (cfg.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ExecutionNotifications:BaseUrl no está configurado.");

        foreach (var lead in leadSet)
        {
            var targetDay = nowTz.Date.AddDays(lead);

            _logger.LogInformation("ExecutionDateNotificationJob: buscando documentos con FechaEjecucion = {Target:yyyy-MM-dd} (faltan {Lead} días, TZ {TZ})",
                targetDay, lead, tz.Id);

            // Traer documentos cuya fecha (parte de fecha) sea exactamente el target
            var docs = await db.Documentos
                .AsNoTracking()
                .Include(d => d.Tareas).ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Include(d => d.IdEmpresaNavigation)
                .Where(d => d.FechaEjecucion != null &&
                            d.FechaEjecucion.Value.Date == targetDay)
                .ToListAsync(ct);

            if (docs.Count == 0)
            {
                _logger.LogInformation("ExecutionDateNotificationJob: 0 documentos para {Lead} días.", lead);
                continue;
            }

            foreach (var d in docs)
            {
                try
                {
                    var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Técnicos asignados (rol 2) por tareas
                    if (cfg.IncludeTechs && d.Tareas != null)
                    {
                        var techs = d.Tareas
                            .Where(t => t.IdTecnicoAsignado != null && t.IdTecnicoAsignadoNavigation != null)
                            .Select(t => t.IdTecnicoAsignadoNavigation!)
                            .Distinct();

                        foreach (var t in techs)
                        {
                            if ((t.IdRol ?? 0) != 3 && !string.IsNullOrWhiteSpace(t.Correo))
                                recipients.Add(t.Correo!.Trim());
                        }
                    }

                    // Admins (rol 1)
                    if (cfg.IncludeAdminsAlways)
                    {
                        var admins = await ActiveEmailsByRoles(db, 1).ToListAsync(ct);
                        foreach (var a in admins) recipients.Add(a);
                    }

                    if (!recipients.Any())
                    {
                        _logger.LogInformation("Documento {Id} sin destinatarios válidos. Se omite (lead {Lead}).", d.IdDocumento, lead);
                        continue;
                    }

                    var empresa = d.IdEmpresaNavigation?.Nombre ?? "(Sin empresa)";
                    var descripcion = d.Descripcion;
                    var docLabel = string.IsNullOrWhiteSpace(d.NumeroDocumento)
                        ? $"ID {d.IdDocumento}"
                        : d.NumeroDocumento!.Trim();

                    // URL al detalle (ajústalo si tienes ruta específica por id)
                    var detalleUrl = $"{baseUrl}/Dashboard/Detalle/{d.IdDocumento}";

                    // Redirección de pruebas (si está configurada)
                    IEnumerable<string> finalRecipients = recipients;
                    if (!string.IsNullOrWhiteSpace(cfg.DevOverrideTo))
                        finalRecipients = new[] { cfg.DevOverrideTo! };

                    await _email.SendExecutionReminderAsync(
                        finalRecipients,
                        docLabel,
                        d.FechaEjecucion!.Value,
                        detalleUrl,
                        empresa,
                        descripcion,
                        leadDays: lead
                    );

                    _logger.LogInformation("Notificación FechaEjecucion (lead {Lead}) enviada por Doc #{DocId} a {Count} destinatarios.",
                        lead, d.IdDocumento, finalRecipients.Count());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error notificando Documento #{DocId} (lead {Lead})", d.IdDocumento, lead);
                }
            }
        }
    }

    /// <summary>
    /// Emails activos por rol (evita rol 3/estado inactivo).
    /// </summary>
    private static IQueryable<string> ActiveEmailsByRoles(OrganizacionalContext db, params int[] roles)
    {
        // Nota: en tu modelo Usuario.Estado es string. Usamos 'Activo' si aplica, o ignoramos nulos.
        return db.Usuarios
            .AsNoTracking()
            .Where(u => u.Correo != null &&
                        u.Correo != "" &&
                        u.IdRol != null &&
                        roles.Contains(u.IdRol.Value) &&
                        (u.Estado == null || u.Estado == "Activo"))
            .Select(u => u.Correo!);
    }
}
