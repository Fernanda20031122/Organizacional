using Organizacional.Data;
using Microsoft.EntityFrameworkCore;
using Organizacional.Services;

public class MaintenanceNotificationsOptions
{
    public int LeadDays { get; set; } = 7;

    // Modo periódico (fallback)
    public int CheckIntervalMinutes { get; set; } = 60;
    public bool RunImmediatelyOnStartup { get; set; } = false;

    // === NUEVO: modo diario fijo ===
    public bool UseDailySchedule { get; set; } = true;       // corre a una hora fija
    public string DailyTime { get; set; } = "06:00";         // formato 24h "HH:mm"
    public string TimeZone { get; set; } = "America/Bogota"; // zona horaria IANA

    public string? PublicBaseUrl { get; set; }

    // Redirección de pruebas opcional
    public string? DevOverrideTo { get; set; }
}

public class MaintenanceNotificationJob
{
    private readonly ILogger<MaintenanceNotificationJob> _logger;

    public MaintenanceNotificationJob(ILogger<MaintenanceNotificationJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteOnceAsync(OrganizacionalContext db, EmailService mail, MaintenanceNotificationsOptions opt, CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(opt.TimeZone ?? TimeZoneInfo.Local.Id);
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        var end = todayLocal.AddDays(opt.LeadDays);

        // ANTES: PlannedDate == target
        // AHORA: ventana [hoy .. hoy+LeadDays]
        var due = await db.MaintenanceSchedules
            .Include(m => m.Document)
                .ThenInclude(d => d.IdEmpresaNavigation)
            .Where(m => !m.IsCompleted
                    && !m.Notified7d
                    && m.PlannedDate != null
                    && m.PlannedDate >= todayLocal
                    && m.PlannedDate <= end)
            .ToListAsync(ct);

        foreach (var m in due)
        {
            var recipients = await GetRecipientsForDocumentAsync(db, m.DocumentoId, ct);
            if (recipients.Length == 0) continue;

            var numero      = m.Document?.NumeroDocumento ?? $"ID {m.DocumentoId}";
            var empresa     = m.Document?.IdEmpresaNavigation?.Nombre;
            var descripcion = m.Document?.Descripcion;

            // Delta (lo puedes seguir usando si quieres en otro lado)
            var deltaDays = (m.PlannedDate!.Value.Date - todayLocal).Days;
            var when = deltaDays == 0 ? "HOY"
                    : deltaDays == 1 ? "en 1 día"
                    : $"en {deltaDays} días";

            // URL ABSOLUTA
            var baseUrl = opt.PublicBaseUrl?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException(
                    "MaintenanceNotifications:PublicBaseUrl no está configurado.");

            var detalleUrl = $"{baseUrl}/Dashboard/Detalle/{m.DocumentoId}";

            await mail.SendMaintenanceReminderAsync(
                recipients,
                numero,
                m.Seq,
                m.PlannedDate!.Value,
                detalleUrl,
                empresa,
                descripcion
            );

            m.Notified7d = true;
        }
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<string> ActiveEmailsByRoles(OrganizacionalContext db, params int[] roles)
    {
        return db.Usuarios
            .Where(u => u.Estado == "activo"
                     && u.Correo != null && u.Correo != ""
                     && roles.Contains(u.IdRol ?? 0))
            .Select(u => u.Correo!);
    }

    private static async Task<string[]> GetRecipientsForDocumentAsync(OrganizacionalContext db, int documentoId, CancellationToken ct)
    {
        int? techId = await db.Tareas
            .Where(t => t.IdDocumento == documentoId && t.IdTecnicoAsignado != null)
            .OrderByDescending(t => t.FechaAsignacion)
            .Select(t => t.IdTecnicoAsignado)
            .FirstOrDefaultAsync(ct);

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (techId.HasValue)
        {
            var tech = await db.Usuarios
                .Where(u => u.IdUsuario == techId.Value && u.Estado == "activo" && u.Correo != null && u.Correo != "")
                .Select(u => u.Correo!)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(tech)) recipients.Add(tech);

            var admins = await ActiveEmailsByRoles(db, 1).ToListAsync(ct);
            foreach (var a in admins) recipients.Add(a);
        }
        else
        {
            var r1 = await ActiveEmailsByRoles(db, 1).ToListAsync(ct);
            var r2 = await ActiveEmailsByRoles(db, 2).ToListAsync(ct);
            foreach (var e in r1) recipients.Add(e);
            foreach (var e in r2) recipients.Add(e);
        }

        return recipients.ToArray();
    }
}