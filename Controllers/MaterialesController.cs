using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;
using System.Text.Json;
using System.Diagnostics;

namespace Organizacional.Controllers
{
    public class MaterialesController : Controller
    {
        private readonly OrganizacionalContext _context;

        public MaterialesController(OrganizacionalContext context)
        {
            _context = context;
        }

        // ✅ LISTA DE MATERIALES PENDIENTES DE ENTREGA (con filtros)
        public async Task<IActionResult> Index(string? empresa, string? estado, string? tipo)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            IQueryable<Documento> query = _context.Documentos
                .Where(d => d.MaterialesPendientes.Any(m => !m.MaterialEntregado))
                .Include(d => d.IdEmpresaNavigation)
                .Include(d => d.MaterialesPendientes)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation);

            // 🔒 Filtrar si es cliente
            if (rol == 3 && idEmpresaSesion.HasValue)
            {
                query = query.Where(d => d.IdEmpresa == idEmpresaSesion.Value);
            }

            // 🔍 Filtro por empresa (solo si NO es cliente)
            if (rol != 3 && !string.IsNullOrEmpty(empresa))
            {
                query = query.Where(d => d.IdEmpresaNavigation.Nombre == empresa);
            }

            // 🔍 Filtro por estado
            if (!string.IsNullOrEmpty(estado))
            {
                query = query.Where(d => d.Tareas.Any(t => t.Estado == estado));
            }

            // 🔍 Filtro por tipo de servicio
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

            var pendientes = pendientesDb.Select(d => new MaterialesPorEntregarViewModel
            {
                IdPendiente = d.IdDocumento,
                NumeroDocumento = d.NumeroDocumento,
                EmpresaNombre = d.IdEmpresaNavigation?.Nombre ?? "Sin empresa",
                FechaRegistro = (d.MaterialesPendientes != null && d.MaterialesPendientes.Any())
                    ? d.MaterialesPendientes.OrderBy(m => m.FechaRegistro).Select(m => m.FechaRegistro).FirstOrDefault()
                    : (DateTime?)null,
                TecnicoAsignado = (d.Suministro.GetValueOrDefault() || d.Instalacion.GetValueOrDefault() || d.Mantenimiento.GetValueOrDefault() || d.Soporte.GetValueOrDefault())
                    ? d.Tareas.FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado"
                    : "N/A",
                Tipo = d.TipoDocumento ?? "sin tipo",
                Suministro = d.Suministro ?? false,
                Instalacion = d.Instalacion ?? false,
                Mantenimiento = d.Mantenimiento ?? false,
                Soporte = d.Soporte ?? false
            }).ToList();

            // 📌 Cargar lista de empresas para el filtro
            if (rol != 3)
            {
                ViewBag.Empresas = await _context.Empresas
                    .Select(e => e.Nombre)
                    .OrderBy(n => n)
                    .ToListAsync();
            }
            else
            {
                ViewBag.Empresas = new List<string>();
            }

            ViewBag.EmpresaSeleccionada = empresa;
            ViewBag.EsCliente = (rol == 3);

            // Si es cliente → auto-seleccionar su empresa
            if (rol == 3 && idEmpresaSesion.HasValue && string.IsNullOrEmpty(empresa))
            {
                var empresaCliente = await _context.Empresas
                    .Where(e => e.IdEmpresa == idEmpresaSesion.Value)
                    .Select(e => e.Nombre)
                    .FirstOrDefaultAsync();

                ViewBag.EmpresaSeleccionada = empresaCliente;
            }

            return View(pendientes);
        }

        // ✅ CARGAR LISTA DE MATERIALES EN EL MODAL
        public async Task<IActionResult> ListaMateriales(int idPendiente)
        {
            var materiales = await _context.MaterialesPendientes
                .Where(m => m.IdDocumento == idPendiente && m.EsSolicitado && !m.MaterialEntregado)
                .ToListAsync();

            return PartialView("_ListaMateriales", materiales);
        }

        // ✅ REGISTRAR MATERIALES (lo que ya tenías en Dashboard)
        [HttpPost]
        public async Task<IActionResult> RegistrarMaterial(int IdDocumento, string NombreMaterial, bool EsSolicitado = true)
        {
            if (IdDocumento <= 0 || string.IsNullOrWhiteSpace(NombreMaterial))
            {
                TempData["Error"] = "Datos inválidos.";
                return RedirectToAction("Detalle", "Dashboard", new { id = IdDocumento });
            }

            var separadores = new[] { ',', ';', '\n', '\r' };
            var nombres = NombreMaterial
                .Split(separadores, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (!nombres.Any())
            {
                TempData["Error"] = "No se encontraron materiales válidos.";
                return RedirectToAction("Detalle", "Dashboard", new { id = IdDocumento });
            }

            var ahora = DateTime.Now;
            var lista = new List<MaterialesPendiente>();
            foreach (var n in nombres)
            {
                var m = new MaterialesPendiente
                {
                    IdDocumento = IdDocumento,
                    NombreMaterial = n,
                    EsSolicitado = EsSolicitado,
                    FechaRegistro = ahora,
                    MaterialEntregado = false
                };
                lista.Add(m);
            }

            _context.MaterialesPendientes.AddRange(lista);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Se registraron {lista.Count} material(es).";
            return RedirectToAction("Detalle", "Dashboard", new { id = IdDocumento });
        }

        // ✅ ACTUALIZAR ENTREGA DESDE EL MODAL
            [HttpPost]
        public IActionResult ActualizarEntregaMateriales(int idDocumento, int[] materialesEntregados)
        {
            if (materialesEntregados != null && materialesEntregados.Length > 0)
            {
                var materiales = _context.MaterialesPendientes
                    .Where(m => m.IdDocumento == idDocumento && materialesEntregados.Contains(m.Id))
                    .ToList();

                foreach (var material in materiales)
                {
                    material.MaterialEntregado = true;
                }

                _context.SaveChanges();
            }

            return RedirectToAction("Index"); // vuelve a la lista de pendientes
        }
    }
}