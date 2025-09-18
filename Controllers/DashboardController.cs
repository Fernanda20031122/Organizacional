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
    public class DashboardController : Controller
    {
        private readonly OrganizacionalContext _context;

        public DashboardController(OrganizacionalContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index() // Vista de Dashboard Principal
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario") ?? 0;
            var rol = HttpContext.Session.GetInt32("Rol");

            if (idUsuario == 0 || rol == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var documentos = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Where(d => !d.Tareas.Any() || d.Tareas.All(t => t.Estado != "Completado" && t.Estado != "Cancelado"))
                .Include(d => d.IdEmpresaNavigation) // 👈 incluir la empresa
                .ToListAsync();

            var modelo = documentos.Select(d => new DashboardItemViewModel
            {
                Estado = d.Tareas.FirstOrDefault()?.Estado ?? "Pendiente",
                FechaInicio = d.FechaInicio?.ToDateTime(TimeOnly.MinValue),
                FechaFin = d.FechaFin?.ToDateTime(TimeOnly.MinValue),
                IdDocumento = d.IdDocumento,
                EmpresaNombre = d.IdEmpresaNavigation?.Nombre ?? "Sin empresa",
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

                TecnicoAsignado = (d.Suministro.GetValueOrDefault() || d.Instalacion.GetValueOrDefault() || d.Mantenimiento.GetValueOrDefault() || d.Soporte.GetValueOrDefault())
                    ? d.Tareas.FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado"
                    : "N/A",

                ColaboradorAsignado = d.Tareas.FirstOrDefault(t => t.IdColaboradorAsignadoNavigation != null)?.IdColaboradorAsignadoNavigation?.Nombre ?? "No asignado",

                Suministro = d.Suministro ?? false,
                Instalacion = d.Instalacion ?? false,
                Mantenimiento = d.Mantenimiento ?? false,
                Soporte = d.Soporte ?? false

            }).ToList();

            // Ordenar: contratos primero (por mayor progreso), luego órdenes (por más días transcurridos)
            modelo = modelo
                .OrderBy(d => d.Tipo != "Contrato") // contratos primero
                .ThenByDescending(d => d.Tipo == "Contrato" ? d.PorcentajeProgreso : 0) // contratos por progreso
                .ThenByDescending(d => d.Tipo != "Contrato" ? d.DiasTranscurridos : 0) // órdenes por días transcurridos
                .ToList();

            return View(modelo);
        }

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var tecnicos = await _context.Usuarios
                .Where(u => u.IdRol == 2 && u.Estado == "activo")
                .ToListAsync();

            var colaboradores = await _context.Usuarios
                .Where(u => u.IdRol == 1 && u.Estado == "activo")
                .ToListAsync();

            var empresas = await _context.Empresas
                .OrderBy(e => e.Nombre)
                .ToListAsync();

            ViewBag.Tecnicos = new SelectList(tecnicos, "IdUsuario", "Nombre");
            ViewBag.Colaboradores = new SelectList(colaboradores, "IdUsuario", "Nombre");
            ViewBag.Empresas = new SelectList(empresas, "IdEmpresa", "Nombre");

            return View(new DocumentoFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(DocumentoFormViewModel modelo) // Vista de Subir Pendiente
        {
            // Validar tipo de documento
            if (string.IsNullOrEmpty(modelo.TipoDocumento) ||
                !(new[] { "Contrato", "Orden", "Otro" }.Contains(modelo.TipoDocumento)))
            {
                ModelState.AddModelError("TipoDocumento", "Tipo de documento inválido.");
            }

            // Validar fechas según el tipo de documento
            if (modelo.TipoDocumento == "Contrato")
            {
                if (modelo.FechaInicio == null || modelo.FechaFin == null)
                {
                    ModelState.AddModelError("", "Debes ingresar fecha de inicio y fin para un contrato.");
                }
            }
            else
            {
                if (modelo.FechaGeneracion == null)
                {
                    ModelState.AddModelError("FechaGeneracion", "La fecha de generación es obligatoria.");
                }
            }

            // NO validamos archivos como obligatorios

            // Si hay errores, recargar ViewBag y volver a la vista
            if (!ModelState.IsValid)
            {
                var tecnicos = await _context.Usuarios.Where(u => u.IdRol == 2 && u.Estado == "activo").ToListAsync();
                var colaboradores = await _context.Usuarios.Where(u => u.IdRol == 1 && u.Estado == "activo").ToListAsync();
                var empresas = await _context.Empresas.ToListAsync();
                ViewBag.Tecnicos = new SelectList(tecnicos, "IdUsuario", "Nombre");
                ViewBag.Colaboradores = new SelectList(colaboradores, "IdUsuario", "Nombre");
                ViewBag.Empresas = new SelectList(empresas, "IdEmpresa", "Nombre");
                return View(modelo);
            }

            var idUsuarioSubio = HttpContext.Session.GetInt32("IdUsuario") ?? 0;

            var documento = new Documento
            {
                TipoDocumento = modelo.TipoDocumento,
                NumeroDocumento = modelo.NumeroDocumento,
                Descripcion = modelo.Descripcion,
                IdEmpresa = modelo.IdEmpresa,
                Suministro = modelo.Suministro,
                Instalacion = modelo.Instalacion,
                Mantenimiento = modelo.Mantenimiento,
                Soporte = modelo.Soporte,
                FechaSubida = DateOnly.FromDateTime(DateTime.Today),
                Asignada = false,
                IdUsuarioSubio = idUsuarioSubio
            };

            // Asignar fechas según tipo
            if (modelo.TipoDocumento == "Contrato")
            {
                documento.FechaInicio = modelo.FechaInicio;
                documento.FechaFin = modelo.FechaFin;
            }
            else
            {
                documento.FechaGeneracion = modelo.FechaGeneracion;
            }

            // Guardar archivo principal si existe
            if (modelo.ArchivoPdf != null && modelo.ArchivoPdf.Length > 0)
            {
                var nombreArchivo = Guid.NewGuid() + Path.GetExtension(modelo.ArchivoPdf.FileName);
                var ruta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", nombreArchivo);
                using var stream = new FileStream(ruta, FileMode.Create);
                await modelo.ArchivoPdf.CopyToAsync(stream);
                documento.ArchivoUrl = "/uploads/" + nombreArchivo;
            }

            // Guardar archivo cotización si existe
            if (modelo.ArchivoCotizacionPdf != null && modelo.ArchivoCotizacionPdf.Length > 0)
            {
                var nombreCot = Guid.NewGuid() + Path.GetExtension(modelo.ArchivoCotizacionPdf.FileName);
                var rutaCot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", nombreCot);
                using var streamCot = new FileStream(rutaCot, FileMode.Create);
                await modelo.ArchivoCotizacionPdf.CopyToAsync(streamCot);
                documento.CotizacionArchivoUrl = "/uploads/" + nombreCot;
                documento.CotizacionFecha = DateTime.Today;
            }

            // Guardar documento
            _context.Documentos.Add(documento);
            await _context.SaveChangesAsync();

            // Crear tarea si hay asignación
            if (modelo.IdTecnicoAsignado.HasValue || modelo.IdColaboradorAsignado.HasValue)
            {
                var tarea = new Tarea
                {
                    IdDocumento = documento.IdDocumento,
                    IdTecnicoAsignado = modelo.IdTecnicoAsignado,
                    IdColaboradorAsignado = modelo.IdColaboradorAsignado,
                    FechaAsignacion = DateOnly.FromDateTime(DateTime.Today),
                    Estado = "Pendiente",
                    Completada = false
                };
                _context.Tareas.Add(tarea);
                await _context.SaveChangesAsync();
            }

            // Guardar info de mantenimiento si aplica
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

        public async Task<IActionResult> Detalle(int id) // Vista Detalle del Pendiente
        {
            var documento = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.MaterialesPendientes)
                .Include(d => d.HerramientaRecogida)
                    .ThenInclude(h => h.IdUsuarioNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdColaboradorAsignadoNavigation)
                .Include(d => d.Mantenimientos)
                .Include(d => d.IdEmpresaNavigation) // 👈 aquí cargas la empresa
                .FirstOrDefaultAsync(d => d.IdDocumento == id);

            if (documento == null)
                return NotFound();

            var viewModel = new DetalleDocumentoViewModel
            {
                Documento = documento,
                EmpresaNombre = documento.IdEmpresaNavigation?.Nombre ?? "Sin empresa",
                Tareas = documento.Tareas.ToList(),
                Materiales = documento.MaterialesPendientes.ToList(),
                UsuarioActual = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.IdUsuario == documento.IdUsuarioSubio) ?? new Usuario()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> CambiarEstadoTarea(int idTarea, string nuevoEstado) // Cambiar el estado del pendiente
        {
            var tarea = await _context.Tareas
                .Include(t => t.IdDocumentoNavigation)
                .FirstOrDefaultAsync(t => t.IdTarea == idTarea);

            if (tarea == null)
                return NotFound();

            // Cambiar estado de la tarea
            tarea.Estado = nuevoEstado;

            // ✅ Si el estado es "Cancelado" o "Completado", guardamos la fecha de cierre en el documento
            if (nuevoEstado.Equals("Cancelado", StringComparison.OrdinalIgnoreCase) ||
                nuevoEstado.Equals("Completado", StringComparison.OrdinalIgnoreCase))
            {
                var colombiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                tarea.IdDocumentoNavigation.FechaCierre = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, colombiaTimeZone);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Detalle", new { id = tarea.IdDocumento });
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
        public async Task<IActionResult> AsignarTecnico(int idDocumento, int idTecnico) // Asignar Tecnico
        {
            var documento = await _context.Documentos.FindAsync(idDocumento);
            if (documento == null)
                return NotFound();

            // Buscar si ya existe una tarea asociada al documento
            var tarea = await _context.Tareas
                .FirstOrDefaultAsync(t => t.IdDocumento == idDocumento);

            if (tarea != null)
            {
                // ✅ Actualizar tarea existente
                tarea.IdTecnicoAsignado = idTecnico;
                _context.Tareas.Update(tarea);
            }
            else
            {
                // ⚠️ Solo si no existe, creamos una nueva
                tarea = new Tarea
                {
                    IdDocumento = idDocumento,
                    IdTecnicoAsignado = idTecnico,
                    Completada = false
                };
                _context.Tareas.Add(tarea);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Detalle), new { id = idDocumento });
        }

        [HttpGet]
        public async Task<IActionResult> AsignarColaborador(int id)
        {
            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
                return NotFound();

            var colaboradores = await _context.Usuarios
                .Where(u => u.IdRol == 1)
                .Select(u => new SelectListItem
                {
                    Value = u.IdUsuario.ToString(),
                    Text = u.Nombre
                }).ToListAsync();

            ViewBag.IdDocumento = id;
            ViewBag.Colaboradores = colaboradores;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarColaborador(int idDocumento, int idColaborador) // Asignar Colaborador
        {
            var documento = await _context.Documentos
                .Include(d => d.Tareas)
                .FirstOrDefaultAsync(d => d.IdDocumento == idDocumento);

            if (documento == null) return NotFound();

            var tarea = documento.Tareas.FirstOrDefault() ?? new Tarea { IdDocumento = idDocumento };

            tarea.IdColaboradorAsignado = idColaborador;
            tarea.FechaAsignacion = DateOnly.FromDateTime(DateTime.Today);

            if (tarea.IdTarea == 0)
                _context.Tareas.Add(tarea);

            await _context.SaveChangesAsync();
            return RedirectToAction("Detalle", new { id = idDocumento });
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
        public async Task<IActionResult> RegistrarMantenimientoPost(int id, DateTime? proxima) // Fechas de los Mantenimientos
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

            // Obtener el documento relacionado
            var documento = await _context.Documentos
                .Include(d => d.Tareas)
                .FirstOrDefaultAsync(d => d.IdDocumento == mantenimiento.IdDocumento);
                
            return RedirectToAction("Detalle", new { id = mantenimiento.IdDocumento });
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarProximoMantenimiento(int id, DateOnly? nuevaFecha) // Proxima Fecha
        {
            var mantenimiento = await _context.Mantenimientos.FindAsync(id);
            if (mantenimiento == null)
                return NotFound();

            mantenimiento.ProximoMantenimiento = nuevaFecha;
            await _context.SaveChangesAsync();

            return RedirectToAction("Detalle", new { id = mantenimiento.IdDocumento });
        }

        [HttpGet]
        public async Task<IActionResult> Historial()
        {
            var pendientes = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Where(d => d.Tareas.Any(t => t.Estado == "Completado" || t.Estado == "Cancelado"))
                .Include(d => d.IdEmpresaNavigation) // 👈 incluir la empresa
                .ToListAsync();

            var modelo = pendientes.Select(d => new DashboardItemViewModel
            {
                Estado = d.Tareas.FirstOrDefault()?.Estado ?? "Pendiente",
                FechaInicio = d.FechaInicio?.ToDateTime(TimeOnly.MinValue),
                FechaFin = d.FechaFin?.ToDateTime(TimeOnly.MinValue),
                IdDocumento = d.IdDocumento,
                Tipo = d.TipoDocumento ?? "Sin tipo",
                NumeroDocumento = d.NumeroDocumento ?? "Sin número",
                SubidoPor = d.IdUsuarioSubioNavigation?.Nombre ?? "Desconocido",
                FechaSubida = d.FechaSubida?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue,
                FechaCierre = d.FechaCierre,
                Suministro = d.Suministro ?? false,
                Instalacion = d.Instalacion ?? false,
                Mantenimiento = d.Mantenimiento ?? false,
                Soporte = d.Soporte ?? false,
                TecnicoAsignado = d.Tareas.FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado",
                EmpresaNombre = d.IdEmpresaNavigation?.Nombre ?? "Sin empresa",
            }).ToList();
            return View(modelo);
        }
    }
}
