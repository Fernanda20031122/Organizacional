using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using System.Data;

namespace Organizacional.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class PendientesController : ControllerBase
    {
        private readonly OrganizacionalContext _context;
        private readonly IConfiguration _cfg;
        public PendientesController(OrganizacionalContext ctx, IConfiguration cfg)
        { _context = ctx; _cfg = cfg; }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> Crear([FromBody] CrearPendienteDto dto)
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) ||
                key != _cfg["ApiKeys:TenereIncoming"])
                return Unauthorized(new { error = "api_key_invalid" });

            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            // Validar empresa
            var empresaOk = await _context.Empresas.AnyAsync(e => e.IdEmpresa == dto.IdEmpresa);
            if (!empresaOk) return BadRequest(new { error = "empresa_invalida" });

            // --- Generar consecutivo en la MISMA conexión (sin transacción explícita) ---
            await _context.Database.OpenConnectionAsync();
            var conn = _context.Database.GetDbConnection();

            // 1) Sube el valor y fija LAST_INSERT_ID(valor + 1)
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE consecutivos SET valor = LAST_INSERT_ID(valor + 1) WHERE nombre='documento_api';";
                await cmd.ExecuteNonQueryAsync();
            }

            // 2) Lee el LAST_INSERT_ID() de ESTA conexión
            int consecutivo;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT LAST_INSERT_ID();";
                var obj = await cmd.ExecuteScalarAsync();
                consecutivo = Convert.ToInt32(obj);
            }

            // Ahora crea el Documento con el consecutivo ya seteado
            var now = NowInBogota();

            var documento = new Documento
            {
                IdEmpresa       = dto.IdEmpresa,
                TipoDocumento   = "Otro",                         // fijo
                NumeroDocumento = consecutivo.ToString(),         // autogenerado
                Descripcion     = dto.Descripcion,
                IdUsuarioSubio  = dto.IdUsuarioSubio ?? 12,
                FechaGeneracion = DateOnly.FromDateTime(now),     // hoy
                FechaSubida     = DateOnly.FromDateTime(now),
                Instalacion     = false,
                Mantenimiento   = false,
                Suministro      = false,
                Soporte         = true
            };

            
            _context.Documentos.Add(documento);
            await _context.SaveChangesAsync();

            return Created($"/dashboard/detalle/{documento.IdDocumento}",
                new { idDocumento = documento.IdDocumento, numeroDocumento = documento.NumeroDocumento });
        }

        private static DateTime NowInBogota()
        {
            try { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")); }
            catch { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time")); }
        }
    }

    // (Opcional) listado para el “desplegable” de empresas en WhatsApp
    [ApiController]
    [Route("api/[controller]")]
    public class EmpresasController : ControllerBase
    {
        private readonly OrganizacionalContext _context;
        private readonly IConfiguration _cfg;
        public EmpresasController(OrganizacionalContext ctx, IConfiguration cfg)
        { _context = ctx; _cfg = cfg; }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) ||
                key != _cfg["ApiKeys:TenereIncoming"])
                return Unauthorized(new { error = "api_key_invalid" });

            var empresas = await _context.Empresas
                .OrderBy(e => e.Nombre)
                .Select(e => new { e.IdEmpresa, e.Nombre })
                .ToListAsync();

            return Ok(empresas);
        }
    }

    // DTO como clase (no record)
    public class CrearPendienteDto
    {
        public string? Descripcion { get; set; }
        public int IdEmpresa { get; set; }
        public int? IdTecnicoAsignado { get; set; }
        public int? IdColaboradorAsignado { get; set; }
        public int? IdUsuarioSubio { get; set; }
    }
}
