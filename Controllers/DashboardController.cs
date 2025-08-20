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
        public async Task<IActionResult> Index()
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
                .ToListAsync();

            var modelo = documentos.Select(d => new DashboardItemViewModel
            {
                Estado = d.Tareas.FirstOrDefault()?.Estado ?? "Pendiente",
                FechaInicio = d.FechaInicio?.ToDateTime(TimeOnly.MinValue),
                FechaFin = d.FechaFin?.ToDateTime(TimeOnly.MinValue),
                IdDocumento = d.IdDocumento,
                EmpresaDestino = d.EmpresaDestino ?? "Sin empresa",
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

            ViewBag.Tecnicos = new SelectList(tecnicos, "IdUsuario", "Nombre");
            ViewBag.Colaboradores = new SelectList(colaboradores, "IdUsuario", "Nombre");

            return View(new DocumentoFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(DocumentoFormViewModel modelo)
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
                ViewBag.Tecnicos = new SelectList(tecnicos, "IdUsuario", "Nombre");
                ViewBag.Colaboradores = new SelectList(colaboradores, "IdUsuario", "Nombre");
                return View(modelo);
            }

            var idUsuarioSubio = HttpContext.Session.GetInt32("IdUsuario") ?? 0;

            var documento = new Documento
            {
                TipoDocumento = modelo.TipoDocumento,
                NumeroDocumento = modelo.NumeroDocumento,
                Descripcion = modelo.Descripcion,
                EmpresaDestino = modelo.EmpresaDestino,
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

        // Vista de detalle del documento
        public async Task<IActionResult> Detalle(int id)
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
                .FirstOrDefaultAsync(d => d.IdDocumento == id);

            if (documento == null)
                return NotFound();

            return View(documento);
        }

        // POST: Solicitar Materiales
        [HttpPost]
        public async Task<IActionResult> RegistrarMaterial(int IdDocumento, string NombreMaterial, bool EsSolicitado = true)
        {
            if (IdDocumento <= 0)
            {
                TempData["Error"] = "Documento no válido.";
                return RedirectToAction("Detalle", new { id = IdDocumento });
            }

            if (string.IsNullOrWhiteSpace(NombreMaterial))
            {
                TempData["Error"] = "Ingrese al menos un material.";
                return RedirectToAction("Detalle", new { id = IdDocumento });
            }

            // Separadores: coma, punto y coma o salto de línea (acepta ambos formatos)
            var separadores = new[] { ',', ';', '\n', '\r' };
            var nombres = NombreMaterial
                .Split(separadores, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (!nombres.Any())
            {
                TempData["Error"] = "No se encontraron materiales válidos.";
                return RedirectToAction("Detalle", new { id = IdDocumento });
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
                    FechaRegistro = ahora
                };

                // tiene MaterialEntregado, inicialízalo en false:
                // m.MaterialEntregado = false;

                lista.Add(m);
            }

            _context.MaterialesPendientes.AddRange(lista);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Se registraron {lista.Count} material(es).";
            return RedirectToAction("Detalle", new { id = IdDocumento });
        }

        // POST: Registrar herramienta en sitio
        [HttpPost]
        public async Task<IActionResult> RegistrarHerramienta(HerramientaRecogida herramienta)
        {
            if (!string.IsNullOrWhiteSpace(herramienta.NombreHerramienta))
            {
                // Obtenemos el ID del usuario logueado
                int? idUsuario = HttpContext.Session.GetInt32("IdUsuario");

                if (idUsuario == null || idUsuario <= 0)
                {
                    TempData["Error"] = "No se pudo identificar al usuario. Inicie sesión nuevamente.";
                    return RedirectToAction("Login", "Auth");
                }

                var items = herramienta.NombreHerramienta
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                foreach (var item in items)
                {
                    var nuevaHerramienta = new HerramientaRecogida
                    {
                        IdDocumento = herramienta.IdDocumento,
                        NombreHerramienta = item,
                        UbicacionDejado = string.IsNullOrEmpty(herramienta.UbicacionDejado) 
                            ? "No especificada" 
                            : herramienta.UbicacionDejado,
                        IdUsuario = idUsuario.Value, 
                        FechaRegistro = DateTime.Now
                    };

                    _context.HerramientaRecogida.Add(nuevaHerramienta);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Detalle", new { id = herramienta.IdDocumento });
        }

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

            return RedirectToAction("Detalle", new { id = idDocumento });
        }

        [HttpPost]
        public IActionResult ActualizarRecogidaHerramientas(int idDocumento, int[] herramientasRecogidas)
        {
            if (herramientasRecogidas != null && herramientasRecogidas.Length > 0)
            {
                var herramientas = _context.HerramientaRecogida
                    .Where(h => h.IdDocumento == idDocumento && herramientasRecogidas.Contains(h.Id))
                    .ToList();

                foreach (var herramienta in herramientas)
                {
                    herramienta.Recogida = true;
                }

                _context.SaveChanges();
            }

            return RedirectToAction("Detalle", new { id = idDocumento });
        }

        [HttpPost]
        public async Task<IActionResult> CambiarEstadoTarea(int idTarea, string nuevoEstado)
        {
            var tarea = await _context.Tareas
                .Include(t => t.IdDocumentoNavigation)
                .FirstOrDefaultAsync(t => t.IdTarea == idTarea);

            if (tarea == null)
                return NotFound();

            tarea.Estado = nuevoEstado;
            await _context.SaveChangesAsync();

            // Crear notificación para el técnico (si tiene uno asignado)
            if (tarea.IdTecnicoAsignado != null)
            {
                var notificacion = new Notificacione
                {
                    IdUsuario = tarea.IdTecnicoAsignado.Value,
                    Mensaje = $"El estado del pendiente '{tarea.IdDocumentoNavigation?.NumeroDocumento ?? "N/A"}' fue actualizado a '{nuevoEstado}'.",
                    Leida = false,
                    Fecha = DateTime.Now
                };

                _context.Notificaciones.Add(notificacion);
                await _context.SaveChangesAsync();
            }

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

            // Crear notificación para el técnico
            var tecnico = await _context.Usuarios.FindAsync(idTecnico);
            if (tecnico != null)
            {
                var notificacion = new Notificacione
                {
                    IdUsuario = tecnico.IdUsuario,
                    Mensaje = $"Se te ha asignado un nuevo pendiente (ID: {idDocumento})",
                    Leida = false,
                    Fecha = DateTime.Now
                };
                _context.Notificaciones.Add(notificacion);
                await _context.SaveChangesAsync();
            }
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
        public async Task<IActionResult> AsignarColaborador(int idDocumento, int idColaborador)
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

            // Obtener el documento relacionado
            var documento = await _context.Documentos
                .Include(d => d.Tareas)
                .FirstOrDefaultAsync(d => d.IdDocumento == mantenimiento.IdDocumento);

            if (documento != null)
            {
                // Buscar técnico asignado (si hay)
                var tecnicoAsignado = documento.Tareas.FirstOrDefault()?.IdTecnicoAsignado;
                if (tecnicoAsignado.HasValue)
                {
                    var notificacion = new Notificacione
                    {
                        IdUsuario = tecnicoAsignado.Value,
                        Mensaje = $"Se ha registrado un nuevo mantenimiento para el documento {documento.NumeroDocumento}.",
                        Leida = false,
                        Fecha = DateTime.Now
                    };

                    _context.Notificaciones.Add(notificacion);
                }
            }
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

        [HttpGet]
        public async Task<IActionResult> Historial()
        {
            var pendientes = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Where(d => d.Tareas.Any(t => t.Estado == "Completado" || t.Estado == "Cancelado"))
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
                Suministro = d.Suministro ?? false,
                Instalacion = d.Instalacion ?? false,
                Mantenimiento = d.Mantenimiento ?? false,
                TecnicoAsignado = d.Tareas.FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado",
                EmpresaDestino = d.EmpresaDestino ?? "Sin empresa"
            }).ToList();
            return View(modelo);
        }
    }
}

