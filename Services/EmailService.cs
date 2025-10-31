using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using System.Text;

namespace Organizacional.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true; // StartTLS (587) por defecto
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty; // cuenta completa (ej. @gmail.com)
        public string Password { get; set; } = string.Empty; // app password
    }

    public class EmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        private MimeMessage BuildMessage(string to, string subject, string html)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = html,
                TextBody = StripTags(html)
            };

            message.Body = builder.ToMessageBody();
            return message;
        }

        private static string StripTags(string html)
        {
            var sb = new StringBuilder();
            bool inside = false;
            foreach (char c in html)
            {
                if (c == '<') inside = true;
                else if (c == '>') inside = false;
                else if (!inside) sb.Append(c);
            }
            return sb.ToString();
        }

        public async Task SendEmailAsync(string to, string subject, string html)
        {
            var message = BuildMessage(to, subject, html);

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpServer, _settings.Port, _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        // ===== Helpers de branding =====

        private static string BaseTemplate(string title, string preheader, string inner)
        {
    return $@"<!doctype html>
<html lang=""es"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <meta name=""x-apple-disable-message-reformatting"">
  <title>{Escape(title)}</title>
</head>
<body style=""margin:0;padding:0;background:#f6f7f9;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;"">
  <span style=""display:none!important;opacity:0;color:transparent;height:0;width:0;overflow:hidden"">{Escape(preheader)}</span>

  <!-- Wrapper -->
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#f6f7f9;mso-table-lspace:0pt;mso-table-rspace:0pt;"">
    <tr>
      <td align=""center"" style=""padding:24px 12px;"">

        <!--[if mso]>
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""560"">
          <tr><td>
        <![endif]-->

        <div style=""max-width:560px;margin:0 auto;width:100%;"">
          <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%""
                 style=""width:100%;max-width:560px;background:#ffffff;border:1px solid #e8e8e8;border-radius:12px;mso-table-lspace:0pt;mso-table-rspace:0pt;"">
            <!-- Header híbrido con gradient -->
            <tr>
              <td align=""left"" style=""padding:0;border-top-left-radius:12px;border-top-right-radius:12px;"">
                <!--[if mso]>
                <v:rect xmlns:v=""urn:schemas-microsoft-com:vml"" fill=""true"" stroke=""false""
                        style=""width:560px;height:60px;"">
                  <v:fill type=""gradient"" color=""#0ea5e9"" color2=""#2563eb"" angle=""135"" />
                  <v:textbox inset=""24px,16px,24px,16px"">
                    <div style=""color:#ffffff;font-family:Arial,Helvetica,sans-serif;font-size:18px;font-weight:700;"">
                      Tenere
                    </div>
                  </v:textbox>
                </v:rect>
                <![endif]-->

                <!--[if !mso]><!-- -->
                <div style=""background-color:#2563eb;background-image:linear-gradient(135deg,#0ea5e9,#2563eb);
                            padding:16px 24px;color:#ffffff;font-family:Arial,Helvetica,sans-serif;
                            font-size:18px;font-weight:700;border-top-left-radius:12px;border-top-right-radius:12px;"">
                  Tenere
                </div>
                <!--<![endif]-->
              </td>
            </tr>

            <!-- Content -->
            <tr>
              <td style=""padding:24px;color:#111827;font-family:Arial,Helvetica,sans-serif;line-height:1.5;font-size:16px;"">
                {inner}
                <p style=""color:#6b7280;font-size:12px;margin-top:18px;"">Este es un mensaje automático desde Tenere.</p>
              </td>
            </tr>
          </table>
        </div>

        <!--[if mso]>
          </td></tr>
        </table>
        <![endif]-->

      </td>
    </tr>
  </table>
</body>
</html>";
        }

        private static string CtaButton(string url, string label, int width = 240, int height = 44)
        {
    return $@"
<!--[if mso]>
  <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word""
    href=""{Escape(url)}""
    style=""height:{height}px;v-text-anchor:middle;width:{width}px;"" arcsize=""12%"" strokecolor=""#2563eb"" fillcolor=""#2563eb"">
    <w:anchorlock/>
    <center style=""color:#ffffff;font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:700;"">
      {Escape(label)}
    </center>
  </v:roundrect>
<![endif]-->
<!--[if !mso]><!-- -->
  <a href=""{Escape(url)}""
     style=""background:#2563eb;border-radius:10px;color:#ffffff;display:inline-block;
            font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:700;
            line-height:{height}px;text-align:center;text-decoration:none;width:{width}px;"">
     {Escape(label)}
  </a>
<!--<![endif]-->";
        }

        private static string CodeBlock(string code)
        {
    // Bloque de una fila completa -> evita que el texto se “suba” a la derecha en Outlook iOS
    return $@"
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:0;padding:0;"">
  <tr>
    <td align=""left"" style=""padding:0;margin:0;"">
      <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" align=""left"" style=""margin:12px 0 12px 0;"">
        <tr>
          <td bgcolor=""#0f172a"" style=""border-radius:12px;padding:12px 16px;text-align:center;mso-padding-alt:12px 16px 12px 16px;"">
            <!--[if mso]>
            <span style=""color:#e2e8f0;font-family:Consolas,Menlo,monospace;font-size:28px;line-height:32px;"">{Escape(code)}</span>
            <![endif]-->
            <!--[if !mso]><!-- -->
            <span style=""color:#e2e8f0;font-family:Consolas,Menlo,Monaco,monospace;font-size:28px;line-height:32px;letter-spacing:0.12em;display:inline-block;"">{Escape(code)}</span>
            <!--<![endif]-->
          </td>
        </tr>
      </table>
    </td>
  </tr>
  <!-- Spacer para “cortar” el flujo en Outlook -->
  <tr><td style=""font-size:0;line-height:0;height:6px;"">&nbsp;</td></tr>
</table>";
        }


        private static string Escape(string? s) => (s ?? string.Empty).Replace("<", "&lt;").Replace(">", "&gt;");

        // ===== Emails públicos =====

        public async Task SendTwoFactorCodeAsync(string to, string code)
        {
            // Mantenerlo simple (como pediste)
            var html = BaseTemplate(
                "Tu código de verificación",
                "Tu código 2FA de Tenere",
                $@"<p style=""margin:0 0 8px 0;"">Tu código de verificación es:</p>
                    {CodeBlock(code)}
                    <p style=""margin:8px 0 0 0;"">Vence en 10 minutos. Si no solicitaste este código, ignora este mensaje.</p>"
            );
            await SendEmailAsync(to, "Tu código de verificación – Tenere", html);
        }

        public Task SendInviteAsync(string to, string nombre, string link)
        {
            var inner = $@"
                <p>Hola {(string.IsNullOrWhiteSpace(nombre) ? "" : $"<b>{Escape(nombre)}</b>")},</p>
                <p>Te creamos una cuenta en <b>Tenere</b>. Para activar tu acceso, define tu contraseña aquí:</p>
                <p style=""margin:20px 0"">{CtaButton(link, "Definir contraseña")}</p>
                <p class=""muted"">El enlace vence en 24 horas.</p>";
            var html = BaseTemplate("Activa tu cuenta", "Activa tu cuenta en Tenere", inner);
            return SendEmailAsync(to, "Activa tu cuenta – Tenere", html);
        }

        public Task SendForgotPasswordAsync(string to, string nombre, string link)
        {
            var inner = $@"
                <p>Hola {(string.IsNullOrWhiteSpace(nombre) ? "" : $"<b>{Escape(nombre)}</b>")},</p>
                <p>Recibimos una solicitud para restablecer tu contraseña en <b>Tenere</b>.</p>
                <p>Si fuiste tú, continúa aquí:</p>
                <p style=""margin:20px 0"">{CtaButton(link, "Restablecer contraseña")}</p>
                <p class=""muted"">Si no solicitaste este cambio, puedes ignorar este correo.</p>";
            var html = BaseTemplate("Restablecer contraseña", "Solicitud de restablecimiento de contraseña", inner);
            return SendEmailAsync(to, "Restablecer contraseña – Tenere", html);
        }

        public record PendienteEmailModel(
            int IdDocumento,
            string? NumeroDocumento,
            string? TipoDocumento,
            string? EmpresaNombre,
            string? CreadoPor,
            DateOnly? FechaSubida,
            bool Suministro,
            bool Instalacion,
            bool Mantenimiento,
            bool Soporte,
            string? Descripcion,
            string UrlDetalle
        );

        public async Task SendPendienteCreadoAsync(IEnumerable<string> recipients, PendienteEmailModel p)
        {
            var chips = new List<string>();
            if (p.Suministro) chips.Add("<span>Suministro</span>");
            if (p.Instalacion) chips.Add("<span>Instalación</span>");
            if (p.Mantenimiento) chips.Add("<span>Mantenimiento</span>");
            if (p.Soporte) chips.Add("<span>Soporte</span>");
            var chipsHtml = chips.Count > 0 ? $@"<div class=""chips"">{string.Join("", chips)}</div>" : "";

            string fecha = p.FechaSubida.HasValue ? p.FechaSubida.Value.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd") : DateTime.Today.ToString("yyyy-MM-dd");

            var inner = $@"
                <p><b>Se creó un nuevo pendiente</b></p>
                <ul style=""padding-left:18px;margin-top:0"">
                  <li><b>Documento:</b> {Escape(p.NumeroDocumento ?? "-")} ({Escape(p.TipoDocumento ?? "Otro")})</li>
                  <li><b>Empresa:</b> {Escape(p.EmpresaNombre ?? "Sin empresa")}</li>
                  <li><b>Creado por:</b> {Escape(p.CreadoPor ?? "-")}</li>
                  <li><b>Fecha:</b> {Escape(fecha)}</li>
                </ul>
                {chipsHtml}
                {(string.IsNullOrWhiteSpace(p.Descripcion) ? "" : $@"<p style=""white-space:pre-wrap;margin-top:14px"">{Escape(p.Descripcion)}</p>")}
                <p style=""margin:20px 0"">{CtaButton(p.UrlDetalle, "Ver pendiente")}</p>";

            var html = BaseTemplate("Nuevo pendiente creado", "Se creó un nuevo pendiente en Tenere", inner);

            var unique = new HashSet<string>(recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!.Trim()), StringComparer.OrdinalIgnoreCase);
            foreach (var to in unique)
            {
                await SendEmailAsync(to, "Nuevo pendiente – Tenere", html);
            }
        }

        public Task SendMaintenanceReminderAsync(
            IEnumerable<string> recipients,
            string documento,
            int seq,
            DateTime plannedDate,
            string detalleUrl,
            string? empresa = null,
            string? descripcion = null)
        {
            // Bloques opcionales
            var empresaBlock = string.IsNullOrWhiteSpace(empresa)
                ? ""
                : $"<p><b>Empresa:</b> {Escape(empresa)}</p>";

            // Muestra la descripción con saltos preservados
            var descBlock = string.IsNullOrWhiteSpace(descripcion)
                ? ""
                : $@"<div style=""margin-top:10px;background:#fafafa;border:1px solid #eee;padding:10px;border-radius:6px;white-space:pre-wrap"">
                        {Escape(descripcion)}
                    </div>";

            var inner = $@"
                <p>Recordatorio de mantenimiento del documento <b>{Escape(documento)}</b>.</p>
                {empresaBlock}
                <p>Mantenimiento <b>#{seq}</b> programado para el <b>{plannedDate:dd/MM/yyyy}</b>.</p>
                {descBlock}
                <p style=""margin:20px 0"">{CtaButton(detalleUrl, "Ver detalle")}</p>";

            // Asunto con empresa y fecha
            var subject = $"[Tenere] Mantenimiento #{seq} – {documento}"
                        + (string.IsNullOrWhiteSpace(empresa) ? "" : $" – {empresa}")
                        + $" – {plannedDate:dd/MM/yyyy}";

            var html = BaseTemplate(
                $"Mantenimiento #{seq} – {documento}",
                $"Recordatorio de mantenimiento para {documento}",
                inner
            );

            var unique = new HashSet<string>(
                recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!.Trim()),
                StringComparer.OrdinalIgnoreCase
            );

            var tasks = unique.Select(to => SendEmailAsync(to, subject, html));
            return Task.WhenAll(tasks);
        }
    }
}