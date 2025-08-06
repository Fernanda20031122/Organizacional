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
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario") ?? 0;
            var rol = HttpContext.Session.GetInt32("Rol");
            if (idUsuario == null || rol == null)
            {
                // Si la sesión expiró o no existe, redirige al login
                return RedirectToAction("Login", "Auth");
            }
            
            ViewBag.Notificaciones = await _context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario)
                .OrderByDescending(n => n.Fecha)
                .Take(5)
                .ToListAsync();

            ViewBag.NotificacionesNoLeidas = await _context.Notificaciones
                .CountAsync(n => n.IdUsuario == idUsuario && n.Leida != true);

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

                TecnicoAsignado = (d.Suministro.GetValueOrDefault() || d.Instalacion.GetValueOrDefault() || d.Mantenimiento.GetValueOrDefault())
                    ? d.Tareas.FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado"
                    : "N/A",

                ColaboradorAsignado = d.Tareas.FirstOrDefault(t => t.IdColaboradorAsignadoNavigation != null)?.IdColaboradorAsignadoNavigation?.Nombre ?? "No asignado",

                // Estos los usas para mostrar viñetas o íconos en la vista
                Suministro = d.Suministro ?? false,
                Instalacion = d.Instalacion ?? false,
                Mantenimiento = d.Mantenimiento ?? false,
                Soporte = d.Soporte ?? false

            }).ToList();

            // ALERTAS AUTOMÁTICAS
            if (idUsuario == 0 || !await _context.Usuarios.AnyAsync(u => u.IdUsuario == idUsuario))
            {
                // No crear notificaciones si el usuario no está autenticado o no existe
                return View(modelo);
            }
            var notificaciones = new List<Notificacione>();

            foreach (var doc in documentos)
            {
                if (doc.TipoDocumento == "Contrato" && doc.FechaFin.HasValue)
                {
                    var diasRestantes = (doc.FechaFin.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
                    if (diasRestantes <= 5 && diasRestantes >= 0)
                    {
                        notificaciones.Add(new Notificacione
                        {
                            IdUsuario = idUsuario,
                            Mensaje = $"El contrato '{doc.NumeroDocumento}' vence en {diasRestantes} días.",
                            Leida = false,
                            Fecha = DateTime.Now
                        });
                    }
                }
            }

            var mantenimientos = await _context.Mantenimientos.Include(m => m.IdDocumentoNavigation).ToListAsync();
            foreach (var m in mantenimientos)
            {
                if (m.ProximoMantenimiento.HasValue)
                {
                    var dias = (m.ProximoMantenimiento.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
                    if (dias < 0)
                    {
                        notificaciones.Add(new Notificacione
                        {
                            IdUsuario = idUsuario,
                            Mensaje = $"El mantenimiento del documento '{m.IdDocumentoNavigation?.NumeroDocumento ?? "Desconocido"}' está vencido.",
                            Leida = false,
                            Fecha = DateTime.Now
                        });
                    }
                    else if (dias <= 3)
                    {
                        notificaciones.Add(new Notificacione
                        {
                            IdUsuario = idUsuario,
                            Mensaje = $"El mantenimiento del documento '{m.IdDocumentoNavigation?.NumeroDocumento ?? "Desconocido"}' será en {dias} días.",
                            Leida = false,
                            Fecha = DateTime.Now
                        });
                    }
                }
            }

            if (notificaciones.Any())
            {
                _context.Notificaciones.AddRange(notificaciones);
                await _context.SaveChangesAsync();
            }
            return View(modelo);
        }

        [HttpPost]
        public async Task<IActionResult> MarcarNotificacionesLeidas()
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null)
                return Unauthorized();

            var noLeidas = await _context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario && n.Leida == false)
                .ToListAsync();

            foreach (var n in noLeidas)
                n.Leida = true;

            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarcarNotificacionesComoLeidas()
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null)
                return Unauthorized();

            var notificaciones = await _context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario && n.Leida == false)
                .ToListAsync();

            foreach (var n in notificaciones)
            {
                n.Leida = true;
            }

            await _context.SaveChangesAsync();
            return Ok(); // Se puede usar para llamadas AJAX
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

        // POST: Solicitar Materiales         // POST: Registrar herramienta en sitio
        [HttpPost]
        public async Task<IActionResult> RegistrarMaterial(MaterialesPendiente material)
        {
            // lógica para materiales solicitados
            material.FechaRegistro = DateTime.Now;
            _context.MaterialesPendientes.Add(material);
            await _context.SaveChangesAsync();

            return RedirectToAction("Detalle", new { id = material.IdDocumento });
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarHerramienta(MaterialesPendiente herramienta)
        {
            // lógica para herramientas dejadas
            herramienta.FechaRegistro = DateTime.Now;
            _context.MaterialesPendientes.Add(herramienta);
            await _context.SaveChangesAsync();

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

