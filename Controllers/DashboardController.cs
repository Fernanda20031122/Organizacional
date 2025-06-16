using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;
using System.Text.Json;

namespace Organizacional.Controllers
{
    public class DashboardController : Controller
    {
        private readonly OrganizacionalContext _context;

        public DashboardController(OrganizacionalContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var documentos = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .ToListAsync();

            var modelo = documentos.Select(d => new DashboardItemViewModel
            {
                Servicios = string.Join(" ",
                    new[]{
                        d.Suministro.GetValueOrDefault() ? "Suministro" : null,
                        d.Instalacion.GetValueOrDefault() ? "Instalación" : null,
                        d.Mantenimiento.GetValueOrDefault() ? "Mantenimiento" : null
                    }.Where(s => s != null)
                ),
                FechaInicio = d.FechaInicio?.ToDateTime(TimeOnly.MinValue),
                FechaFin = d.FechaFin?.ToDateTime(TimeOnly.MinValue),
                IdDocumento = d.IdDocumento,
                Tipo = d.TipoDocumento ?? "Sin tipo",
                NumeroDocumento = d.NumeroDocumento ?? "Sin número",
                SubidoPor = d.IdUsuarioSubioNavigation?.Nombre ?? "Desconocido",
                FechaSubida = d.FechaSubida.HasValue
                    ? d.FechaSubida.Value.ToDateTime(TimeOnly.MinValue)
                    : DateTime.MinValue,

                DiasTranscurridos = (d.FechaGeneracion.HasValue)
                    ? (int)(DateTime.Today - d.FechaGeneracion.Value.ToDateTime(TimeOnly.MinValue)).TotalDays
                    : 0,

                DiasTotalesContrato = (d.FechaInicio.HasValue && d.FechaFin.HasValue)
                    ? (int)(d.FechaFin.Value.ToDateTime(TimeOnly.MinValue) - d.FechaInicio.Value.ToDateTime(TimeOnly.MinValue)).TotalDays
                    : 0,

                DiasTranscurridosContrato = (d.FechaInicio.HasValue && d.FechaFin.HasValue)
                    ? Math.Clamp((int)(DateTime.Today - d.FechaInicio.Value.ToDateTime(TimeOnly.MinValue)).TotalDays, 0,
                        (int)(d.FechaFin.Value.ToDateTime(TimeOnly.MinValue) - d.FechaInicio.Value.ToDateTime(TimeOnly.MinValue)).TotalDays)
                    : 0,

                PorcentajeProgreso = (d.FechaInicio.HasValue && d.FechaFin.HasValue)
                    ? (int)(Math.Clamp((int)(DateTime.Today - d.FechaInicio.Value.ToDateTime(TimeOnly.MinValue)).TotalDays, 0,
                        (int)(d.FechaFin.Value.ToDateTime(TimeOnly.MinValue) - d.FechaInicio.Value.ToDateTime(TimeOnly.MinValue)).TotalDays)
                        * 100 /
                        (int)(d.FechaFin.Value.ToDateTime(TimeOnly.MinValue) - d.FechaInicio.Value.ToDateTime(TimeOnly.MinValue)).TotalDays)
                    : 0,

                TecnicoAsignado = (d.Suministro.GetValueOrDefault() || d.Instalacion.GetValueOrDefault() || d.Mantenimiento.GetValueOrDefault())
                    ? d.Tareas.FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado"
                    : "N/A"
            }).ToList();

            return View(modelo);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            return View(new DocumentoFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(DocumentoFormViewModel modelo, IFormFile archivoPdf, IFormFile archivoCotizacionPdf)
        {
            if (ModelState.IsValid)
            {
                var documento = new Documento
                {
                    TipoDocumento = modelo.TipoDocumento,
                    NumeroDocumento = modelo.NumeroDocumento,
                    Descripcion = modelo.Descripcion,
                    EmpresaDestino = modelo.EmpresaDestino,
                    Suministro = modelo.Suministro,
                    Instalacion = modelo.Instalacion,
                    Mantenimiento = modelo.Mantenimiento,
                    FechaSubida = DateOnly.FromDateTime(DateTime.Today),
                    Asignada = false,
                    IdUsuarioSubio = 1 // Ajusta según autenticación real
                };

                // Asignar fechas según tipo
                if (modelo.TipoDocumento == "Contrato")
                {
                    documento.FechaInicio = modelo.FechaInicio;
                    documento.FechaFin = modelo.FechaFin;
                }
                else if (modelo.FechaGeneracion != null)
                {
                    documento.FechaGeneracion = modelo.FechaGeneracion;
                }
                else
                {
                    ModelState.AddModelError("FechaGeneracion", "La fecha de generación es obligatoria.");
                    return View(modelo);
                }

                // Subir archivo del documento
                if (archivoPdf != null && archivoPdf.Length > 0)
                {
                    var nombreArchivo = Guid.NewGuid() + Path.GetExtension(archivoPdf.FileName);
                    var ruta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", nombreArchivo);
                    using var stream = new FileStream(ruta, FileMode.Create);
                    await archivoPdf.CopyToAsync(stream);
                    documento.ArchivoUrl = "/uploads/" + nombreArchivo;
                }

                // Subir archivo de la cotización
                if (archivoCotizacionPdf != null && archivoCotizacionPdf.Length > 0)
                {
                    var nombreCot = Guid.NewGuid() + Path.GetExtension(archivoCotizacionPdf.FileName);
                    var rutaCot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", nombreCot);
                    using var streamCot = new FileStream(rutaCot, FileMode.Create);
                    await archivoCotizacionPdf.CopyToAsync(streamCot);
                    documento.CotizacionArchivoUrl = "/uploads/" + nombreCot;
                    documento.CotizacionFecha = DateTime.Today;
                }

                _context.Documentos.Add(documento);
                await _context.SaveChangesAsync();

                // Si hay mantenimiento, guardar el registro
                if (modelo.Mantenimiento && modelo.CantidadMantenimientos > 0)
                {
                    var mantenimiento = new Mantenimiento
                    {
                        IdDocumento = documento.IdDocumento,
                        TotalMantenimientos = modelo.CantidadMantenimientos,
                        MantenimientoRealizado = 0,
                        ProximoMantenimiento = null,
                        FechasRealizadasJson = JsonSerializer.Serialize(new List<string>())
                    };
                    _context.Mantenimientos.Add(mantenimiento);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            return View(modelo);
        }

        public async Task<IActionResult> Detalle(int id)
        {
            var documento = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas).ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Include(d => d.Mantenimientos)
                .FirstOrDefaultAsync(d => d.IdDocumento == id);

            if (documento == null)
                return NotFound();

            return View(documento);
        }

        [HttpGet]
        public async Task<IActionResult> AsignarTecnico(int id)
        {
            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
                return NotFound();

            var tecnicos = await _context.Usuarios
                .Where(u => u.IdRol == 2)
                .Select(u => new SelectListItem
                {
                    Value = u.IdUsuario.ToString(),
                    Text = u.Nombre
                }).ToListAsync();

            ViewBag.IdDocumento = id;
            ViewBag.Tecnicos = tecnicos;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarTecnico(int idDocumento, int idTecnico)
        {
            var documento = await _context.Documentos.FindAsync(idDocumento);
            if (documento == null)
                return NotFound();

            var tarea = new Tarea
            {
                IdDocumento = idDocumento,
                IdTecnicoAsignado = idTecnico,
                Completada = false
            };

            _context.Tareas.Add(tarea);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Detalle), new { id = idDocumento });
        }
            
        [HttpGet]
        public async Task<IActionResult> RegistrarMantenimiento(int id)
        {
            var mantenimiento = await _context.Mantenimientos
                .Include(m => m.IdDocumentoNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mantenimiento == null)
                return NotFound();

            return View(mantenimiento);
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarMantenimientoPost(int id, DateTime? proxima)
        {
            var mantenimiento = await _context.Mantenimientos.FindAsync(id);
            if (mantenimiento == null) return NotFound();

            // Deserializar las fechas existentes
            var fechas = string.IsNullOrEmpty(mantenimiento.FechasRealizadasJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(mantenimiento.FechasRealizadasJson);

            // Agregar la nueva fecha
            fechas.Add(DateTime.Now.ToString("yyyy-MM-dd"));

            // Volver a serializar
            mantenimiento.FechasRealizadasJson = JsonSerializer.Serialize(fechas);

            mantenimiento.MantenimientoRealizado++;

            if (proxima.HasValue)
            {
                mantenimiento.ProximoMantenimiento = DateOnly.FromDateTime(proxima.Value);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Detalle", new { id = mantenimiento.IdDocumento });
        }
        [HttpPost]
        public async Task<IActionResult> ActualizarProximoMantenimiento(int id, DateOnly? nuevaFecha)
        {
            var mantenimiento = await _context.Mantenimientos.FindAsync(id);
            if (mantenimiento == null)
                return NotFound();

            mantenimiento.ProximoMantenimiento = nuevaFecha;
            await _context.SaveChangesAsync();

            return RedirectToAction("Detalle", new { id = mantenimiento.IdDocumento });
        }
    }
}
