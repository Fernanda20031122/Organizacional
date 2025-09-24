using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;
using System.Diagnostics;

namespace Organizacional.Controllers
{
    public class HerramientasController : Controller
    {
        private readonly OrganizacionalContext _context;

        public HerramientasController(OrganizacionalContext context)
        {
            _context = context;
        }

        // ✅ LISTA DE HERRAMIENTAS PENDIENTES DE RECOGER CON FILTROS
        public async Task<IActionResult> Index(string? empresa, string? estado, string? tipo)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresa = HttpContext.Session.GetInt32("IdEmpresa");

            IQueryable<Documento> query = _context.Documentos
                .Where(d => d.HerramientaRecogida.Any(h => !h.Recogida))
                .Include(d => d.IdEmpresaNavigation)
                .Include(d => d.HerramientaRecogida)
                    .ThenInclude(h => h.IdUsuarioNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation);

            // 🔒 Filtrar si es cliente
            if (rol == 3 && idEmpresa.HasValue)
            {
                query = query.Where(d => d.IdEmpresa == idEmpresa.Value);
            }

            // 📌 Filtro por empresa (solo si no es cliente)
            if (rol != 3 && !string.IsNullOrEmpty(empresa))
            {
                query = query.Where(d => d.IdEmpresaNavigation.Nombre == empresa);
            }

            // 📌 Filtro por estado (usa estado de las tareas relacionadas)
            if (!string.IsNullOrEmpty(estado))
            {
                query = query.Where(d => d.Tareas.Any(t => t.Estado == estado));
            }

            // 📌 Filtro por tipo de servicio
            if (!string.IsNullOrEmpty(tipo))
            {
                query = query.Where(d =>
                    (tipo == "Suministro" && d.Suministro == true) ||
                    (tipo == "Instalacion" && d.Instalacion == true) ||
                    (tipo == "Mantenimiento" && d.Mantenimiento == true) ||
                    (tipo == "Soporte" && d.Soporte == true)
                );
            }

            var pendientesDb = await query.ToListAsync();

            var pendientes = pendientesDb.Select(d => new HerramientasPorRecogerViewModel
            {
                Id = d.IdDocumento,
                NumeroDocumento = d.NumeroDocumento,
                EmpresaNombre = d.IdEmpresaNavigation?.Nombre ?? "Sin empresa",
                FechaRegistro = (d.HerramientaRecogida != null && d.HerramientaRecogida.Any())
                    ? d.HerramientaRecogida.OrderByDescending(h => h.FechaRegistro).Select(h => h.FechaRegistro).FirstOrDefault()
                    : (DateTime?)null,
                UltimoUsuario = (d.HerramientaRecogida != null && d.HerramientaRecogida.Any())
                    ? d.HerramientaRecogida.OrderByDescending(h => h.FechaRegistro).Select(h => h.IdUsuarioNavigation?.Nombre).FirstOrDefault()
                    : "N/A",
                TecnicoAsignado = (d.Suministro.GetValueOrDefault() || d.Instalacion.GetValueOrDefault() || d.Mantenimiento.GetValueOrDefault() || d.Soporte.GetValueOrDefault())
                    ? d.Tareas.FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado"
                    : "N/A",
                Tipo = d.TipoDocumento ?? "sin tipo",
                Suministro = d.Suministro ?? false,
                Instalacion = d.Instalacion ?? false,
                Mantenimiento = d.Mantenimiento ?? false,
                Soporte = d.Soporte ?? false
            }).ToList();

            // 📌 Enviar lista de empresas al ViewBag para el filtro
            ViewBag.Empresas = await _context.Empresas.Select(e => e.Nombre).ToListAsync();
            ViewBag.EmpresaSeleccionada = empresa;

            return View(pendientes);
        }

        // ✅ CARGAR LISTA DE HERRAMIENTAS EN EL MODAL
        public async Task<IActionResult> ListaHerramientas(int idPendiente)
        {
            var herramientas = await _context.HerramientaRecogida
                .Where(h => h.IdDocumento == idPendiente)
                .ToListAsync();

            return PartialView("_ListaHerramientas", herramientas);
        }

        // ✅ REGISTRAR HERRAMIENTAS
        [HttpPost]
        public async Task<IActionResult> RegistrarHerramienta(int IdDocumento, string NombreHerramienta, string UbicacionDejado)
        {
            if (IdDocumento <= 0 || string.IsNullOrWhiteSpace(NombreHerramienta))
            {
                TempData["Error"] = "Datos inválidos.";
                return RedirectToAction("Detalle", "Dashboard", new { id = IdDocumento });
            }

            var separadores = new[] { ',', ';', '\n', '\r' };
            var nombres = NombreHerramienta
                .Split(separadores, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (!nombres.Any())
            {
                TempData["Error"] = "No se encontraron herramientas válidas.";
                return RedirectToAction("Detalle", "Dashboard", new { id = IdDocumento });
            }

            int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null || idUsuario <= 0)
            {
                TempData["Error"] = "No se pudo identificar al usuario.";
                return RedirectToAction("Login", "Auth");
            }

            var ahora = DateTime.Now;
            var lista = new List<HerramientaRecogida>();
            foreach (var n in nombres)
            {
                var h = new HerramientaRecogida
                {
                    IdDocumento = IdDocumento,
                    NombreHerramienta = n,
                    UbicacionDejado = string.IsNullOrEmpty(UbicacionDejado) ? "No especificada" : UbicacionDejado,
                    IdUsuario = idUsuario.Value,
                    FechaRegistro = ahora,
                    Recogida = false
                };
                lista.Add(h);
            }

            _context.HerramientaRecogida.AddRange(lista);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Se registraron {lista.Count} herramienta(s).";
            return RedirectToAction("Detalle", "Dashboard", new { id = IdDocumento });
        }

        // ✅ ACTUALIZAR RECOGIDA DESDE EL MODAL
        [HttpPost]
        public IActionResult ActualizarRecogidaHerramientas(int idDocumento, int[] herramientasRecogidas)
        {
            if (herramientasRecogidas != null && herramientasRecogidas.Length > 0)
            {
                var herramientas = _context.HerramientaRecogida
                    .Where(h => h.IdDocumento == idDocumento && herramientasRecogidas.Contains(h.Id))
                    .ToList();

                foreach (var h in herramientas)
                {
                    h.Recogida = true;
                }

                _context.SaveChanges();
            }

            return RedirectToAction("Index"); // vuelve a la lista de pendientes
        }
    }
}