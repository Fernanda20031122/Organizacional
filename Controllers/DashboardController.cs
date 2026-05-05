using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;
using Organizacional.Services;
using System.Text.Json;
using System.Diagnostics;
using System.Text;

namespace Organizacional.Controllers
{
    public class DashboardController : Controller
    {
        private readonly OrganizacionalContext _context;
        private readonly EmailService _email;
        private readonly IConfiguration _cfg;

        public DashboardController(OrganizacionalContext context, EmailService email, IConfiguration cfg)
        {
            _context = context;
            _email = email;
            _cfg = cfg;
        }
        public async Task<IActionResult> Index(string? empresa, string? estado, string? tipo)
        {
            var idUsuario  = HttpContext.Session.GetInt32("IdUsuario") ?? 0;
            var rol        = HttpContext.Session.GetInt32("Rol");
            var idEmpresa  = HttpContext.Session.GetInt32("IdEmpresa");

            if (idUsuario == 0 || rol == null)
                return RedirectToAction("Login", "Auth");

            var modelo = await BuildDashboardItems(rol!.Value, idEmpresa, empresa, estado, tipo);

            // ViewBags (idénticos a lo que ya tenías)
            if (rol != 3)
                ViewBag.Empresas = await _context.Empresas.Select(e => e.Nombre).OrderBy(n => n).ToListAsync();
            else
                ViewBag.Empresas = new List<string>();

            ViewBag.EmpresaSeleccionada = empresa;
            ViewBag.EsCliente = (rol == 3);

            if (rol == 3 && idEmpresa.HasValue && string.IsNullOrEmpty(empresa))
            {
                var empresaCliente = await _context.Empresas
                    .Where(e => e.IdEmpresa == idEmpresa.Value)
                    .Select(e => e.Nombre)
                    .FirstOrDefaultAsync();

                ViewBag.EmpresaSeleccionada = empresaCliente;
            }

            return View(modelo);
        }

        private async Task<List<DashboardItemViewModel>> BuildDashboardItems(int rol, int? idEmpresaSesion,
                                                                     string? empresa, string? estado, string? tipo)
        {
            var documentosQuery = _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas).ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Include(d => d.IdEmpresaNavigation)
                .Where(d => !d.Tareas.Any() || d.Tareas.All(t => t.Estado != "Completado" && t.Estado != "Cancelado"));

            if (rol == 3 && idEmpresaSesion.HasValue)
                documentosQuery = documentosQuery.Where(d => d.IdEmpresa == idEmpresaSesion.Value);

            if (rol != 3 && !string.IsNullOrEmpty(empresa))
                documentosQuery = documentosQuery.Where(d => d.IdEmpresaNavigation.Nombre == empresa);

            if (!string.IsNullOrEmpty(estado))
                documentosQuery = documentosQuery.Where(d => d.Tareas.Any(t => t.Estado == estado));

            if (!string.IsNullOrEmpty(tipo))
            {
                documentosQuery = documentosQuery.Where(d =>
                    (tipo == "Suministro"  && d.Suministro  == true) ||
                    (tipo == "Instalacion" && d.Instalacion == true) ||
                    (tipo == "Mantenimiento" && d.Mantenimiento == true) ||
                    (tipo == "Soporte"     && d.Soporte     == true)
                );
            }

            var documentos = await documentosQuery.ToListAsync();

            var modelo = documentos.Select(d => new DashboardItemViewModel
            {
                Estado = d.Tareas.FirstOrDefault()?.Estado ?? "Pendiente",
                FechaEjecucion = d.FechaEjecucion,
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

                Suministro  = d.Suministro  ?? false,
                Instalacion = d.Instalacion ?? false,
                Mantenimiento = d.Mantenimiento ?? false,
                Soporte = d.Soporte ?? false

            }).ToList();

            modelo = modelo
                .OrderBy(d => d.Tipo != "Contrato")
                .ThenByDescending(d => d.Tipo == "Contrato" ? d.PorcentajeProgreso : 0)
                .ThenByDescending(d => d.Tipo != "Contrato" ? d.DiasTranscurridos : 0)
                .ToList();

            return modelo;
        }

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresa = HttpContext.Session.GetInt32("IdEmpresa");

            // ---- VISTA REDUCIDA PARA CLIENTE ----
            if (rol == 3)
            {
                var vm = new SubirPendienteClienteViewModel();

                // Si el cliente tiene empresa fijada en sesión, la usamos
                if (idEmpresa.HasValue)
                {
                    vm.IdEmpresa = idEmpresa.Value;
                }
                else
                {
                    // (Solo si quieres permitir elegir empresa)
                    vm.Empresas = await _context.Empresas
                        .OrderBy(e => e.Nombre)
                        .Select(e => new SelectListItem { Value = e.IdEmpresa.ToString(), Text = e.Nombre })
                        .ToListAsync();
                }

                return View("SubirPendienteCliente", vm);
            }

            // ---- VISTA COMPLETA PARA ADMIN / OTROS ROLES ----
            var tecnicos = await _context.Usuarios
                .Where(u => u.IdRol == 2 && u.Estado == "activo")
                .ToListAsync();

            var colaboradores = await _context.Usuarios
                .Where(u => u.IdRol == 1 && u.Estado == "activo")
                .ToListAsync();

            List<Empresa> empresas;
            if (rol == 3 && idEmpresa.HasValue)
            {
                empresas = await _context.Empresas.Where(e => e.IdEmpresa == idEmpresa.Value).ToListAsync();
            }
            else
            {
                empresas = await _context.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            }

            ViewBag.Tecnicos = new SelectList(tecnicos, "IdUsuario", "Nombre");
            ViewBag.Colaboradores = new SelectList(colaboradores, "IdUsuario", "Nombre");
            ViewBag.Empresas = new SelectList(empresas, "IdEmpresa", "Nombre");

            return View(new DocumentoFormViewModel());
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> PendientesTable(string? empresa, string? estado, string? tipo)
        {
            var rol       = HttpContext.Session.GetInt32("Rol");
            var idEmpresa = HttpContext.Session.GetInt32("IdEmpresa");

            if (rol == null) return Unauthorized();

            var items = await BuildDashboardItems(rol.Value, idEmpresa, empresa, estado, tipo);
            return PartialView("_PendientesTable", items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(DocumentoFormViewModel modelo)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");
            var idUsuarioSubio = HttpContext.Session.GetInt32("IdUsuario") ?? 0;

            // Validación: cliente no puede forzar otra empresa
            if (rol == 3 && idEmpresaSesion.HasValue)
            {
                modelo.IdEmpresa = idEmpresaSesion.Value;
            }

            // Validar tipo de documento
            if (string.IsNullOrEmpty(modelo.TipoDocumento) ||
                !(new[] { "Contrato", "Orden", "Otro" }.Contains(modelo.TipoDocumento)))
            {
                ModelState.AddModelError("TipoDocumento", "Tipo de documento inválido.");
            }

            // Validar fechas
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

            // Si hay errores, recargar ViewBag y volver
            if (!ModelState.IsValid)
            {
                var tecnicos = await _context.Usuarios.Where(u => u.IdRol == 2 && u.Estado == "activo").ToListAsync();
                var colaboradores = await _context.Usuarios.Where(u => u.IdRol == 1 && u.Estado == "activo").ToListAsync();

                List<Empresa> empresas;
                if (rol == 3 && idEmpresaSesion.HasValue)
                {
                    empresas = await _context.Empresas.Where(e => e.IdEmpresa == idEmpresaSesion.Value).ToListAsync();
                }
                else
                {
                    empresas = await _context.Empresas.ToListAsync();
                }

                ViewBag.Tecnicos = new SelectList(tecnicos, "IdUsuario", "Nombre");
                ViewBag.Colaboradores = new SelectList(colaboradores, "IdUsuario", "Nombre");
                ViewBag.Empresas = new SelectList(empresas, "IdEmpresa", "Nombre");

                return View(modelo);
            }

            var documento = new Documento
            {
                TipoDocumento = modelo.TipoDocumento,
                NumeroDocumento = modelo.NumeroDocumento,
                Descripcion = modelo.Descripcion,
                IdEmpresa = modelo.IdEmpresa, // ya validado según rol
                Suministro = modelo.Suministro,
                Instalacion = modelo.Instalacion,
                Mantenimiento = modelo.Mantenimiento,
                Soporte = modelo.Soporte,
                FechaSubida = DateOnly.FromDateTime(DateTime.Today),
                Asignada = false,
                IdUsuarioSubio = idUsuarioSubio
            };

            // Asignar fechas
            if (modelo.TipoDocumento == "Contrato")
            {
                documento.FechaInicio = modelo.FechaInicio;
                documento.FechaFin = modelo.FechaFin;
            }
            else
            {
                documento.FechaGeneracion = modelo.FechaGeneracion;
            }

            // Guardar archivos
            if (modelo.ArchivoPdf != null && modelo.ArchivoPdf.Length > 0)
            {
                var nombreArchivo = Guid.NewGuid() + Path.GetExtension(modelo.ArchivoPdf.FileName);
                var ruta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", nombreArchivo);
                using var stream = new FileStream(ruta, FileMode.Create);
                await modelo.ArchivoPdf.CopyToAsync(stream);
                documento.ArchivoUrl = "/uploads/" + nombreArchivo;
            }

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

            // ====== Schedules (nuevo) ======
            if (modelo.Mantenimiento && (modelo.CantidadMantenimientos ?? 0) > 0)
            {
                var now = DateTime.UtcNow;
                var schedules = new List<MaintenanceSchedule>();
                var count = modelo.CantidadMantenimientos ?? 0;

                var seedDates = new List<DateTime?>();

                if (modelo.TipoDocumento == "Contrato" && modelo.FechaInicio.HasValue && modelo.FechaFin.HasValue)
                {
                    var start = modelo.FechaInicio.Value.ToDateTime(TimeOnly.MinValue);
                    var end   = modelo.FechaFin.Value.ToDateTime(TimeOnly.MinValue);
                    var spanDays = (end - start).TotalDays;
                    var step = spanDays / (count + 1d);   // double

                    for (int i = 1; i <= count; i++)
                    {
                        var offset = step * i;            // double
                        seedDates.Add(start.AddDays(offset));
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                        seedDates.Add(null);              // sin rango -> quedan vacías
                }

                short seq = 1;
                foreach (var pd in seedDates)
                {
                    schedules.Add(new MaintenanceSchedule
                    {
                        DocumentoId = documento.IdDocumento,
                        Seq = seq++,
                        PlannedDate = pd,
                        IsCompleted = false,
                        Notified7d = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }

                _context.MaintenanceSchedules.AddRange(schedules);
                await _context.SaveChangesAsync();
            }

            // ===== Email: nuevo pendiente =====
            const int ROL_ADMIN = 1;

            var creador = await _context.Usuarios
                .Where(u => u.IdUsuario == idUsuarioSubio)
                .Select(u => new { u.Nombre, u.Correo })
                .FirstOrDefaultAsync();

            var adminMails = await _context.Usuarios
                .Where(u => u.IdRol == ROL_ADMIN && u.Estado == "activo" && u.Correo != null && u.Correo != "")
                .Select(u => u.Correo!)
                .ToListAsync();

            var baseUrl = _cfg["PublicBaseUrl"];
            var detalleUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? $"{baseUrl!.TrimEnd('/')}/Dashboard/Detalle/{documento.IdDocumento}"
                : Url.Action("Detalle", "Dashboard", new { id = documento.IdDocumento }, Request.Scheme)!;

            var empresaNombre = await _context.Empresas
                .Where(e => e.IdEmpresa == documento.IdEmpresa)
                .Select(e => e.Nombre)
                .FirstOrDefaultAsync();

            await _email.SendPendienteCreadoAsync(
                new[] { creador?.Correo ?? HttpContext.Session.GetString("Correo") ?? "" }.Concat(adminMails),
                new EmailService.PendienteEmailModel(
                    documento.IdDocumento,
                    documento.NumeroDocumento,
                    documento.TipoDocumento,
                    empresaNombre,
                    creador?.Nombre ?? HttpContext.Session.GetString("Nombre"),
                    documento.FechaSubida,
                    documento.Suministro ?? false,
                    documento.Instalacion ?? false,
                    documento.Mantenimiento ?? false,
                    documento.Soporte ?? false,
                    documento.Descripcion,
                    detalleUrl
                )
            );

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

        // Consecutivo para documentos Tipo = "Otro" (solo numéricos)
        private async Task<string> GenerarConsecutivoOtroAsync()
        {
            var numeros = await _context.Documentos
                .Where(d => d.TipoDocumento == "Otro" && d.NumeroDocumento != null && d.NumeroDocumento != "")
                .Select(d => d.NumeroDocumento!)
                .AsNoTracking()
                .ToListAsync();

            int max = 0;
            foreach (var s in numeros)
            {
                if (int.TryParse(s, out var n) && n > max)
                    max = n;
            }

            // si no hay ninguno, comienza en 1
            return (max + 1).ToString();
        }

        private async Task CargarListasFormularioAsync(int? idTecnicoSeleccionado = null, int? idColaboradorSeleccionado = null, int? idEmpresaSeleccionada = null)
        {
            var tecnicos = await _context.Usuarios
                .Where(u => u.IdRol == 2 && u.Estado == "activo")
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            var colaboradores = await _context.Usuarios
                .Where(u => u.IdRol == 1 && u.Estado == "activo")
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            var empresas = await _context.Empresas
                .OrderBy(e => e.Nombre)
                .ToListAsync();

            ViewBag.Tecnicos = new SelectList(tecnicos, "IdUsuario", "Nombre", idTecnicoSeleccionado);
            ViewBag.Colaboradores = new SelectList(colaboradores, "IdUsuario", "Nombre", idColaboradorSeleccionado);
            ViewBag.Empresas = new SelectList(empresas, "IdEmpresa", "Nombre", idEmpresaSeleccionada);
        }

        private void ValidarArchivoPdf(IFormFile? archivo, string campo)
        {
            if (archivo == null || archivo.Length == 0)
                return;

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (extension != ".pdf")
            {
                ModelState.AddModelError(campo, "Solo se permiten archivos PDF.");
            }
        }

        private async Task<string> GuardarArchivoPdfAsync(IFormFile archivo)
        {
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsPath);

            var nombreArchivo = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName).ToLowerInvariant()}";
            var ruta = Path.Combine(uploadsPath, nombreArchivo);

            await using var stream = new FileStream(ruta, FileMode.Create);
            await archivo.CopyToAsync(stream);

            return "/uploads/" + nombreArchivo;
        }

        private void EliminarArchivoFisico(string? archivoUrl)
        {
            if (string.IsNullOrWhiteSpace(archivoUrl))
                return;

            if (!archivoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return;

            var rutaRelativa = archivoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var rutaCompleta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", rutaRelativa);

            if (System.IO.File.Exists(rutaCompleta))
            {
                System.IO.File.Delete(rutaCompleta);
            }
        }

        private static List<DateTime?> CalcularFechasMantenimiento(DocumentoFormViewModel modelo, int cantidad)
        {
            var fechas = new List<DateTime?>();

            if (modelo.TipoDocumento == "Contrato" && modelo.FechaInicio.HasValue && modelo.FechaFin.HasValue)
            {
                var inicio = modelo.FechaInicio.Value.ToDateTime(TimeOnly.MinValue);
                var fin = modelo.FechaFin.Value.ToDateTime(TimeOnly.MinValue);
                var dias = (fin - inicio).TotalDays;
                var paso = dias / (cantidad + 1d);

                for (var i = 1; i <= cantidad; i++)
                    fechas.Add(inicio.AddDays(paso * i));
            }
            else
            {
                for (var i = 0; i < cantidad; i++)
                    fechas.Add(null);
            }

            return fechas;
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario") ?? 0;
            var rol = HttpContext.Session.GetInt32("Rol");

            if (idUsuario == 0 || rol == null)
                return RedirectToAction("Login", "Auth");

            // Por seguridad, los clientes solo consultan sus pendientes; no los editan desde esta vista.
            if (rol == 3)
                return Forbid();

            var documento = await _context.Documentos
                .Include(d => d.Tareas)
                .Include(d => d.Mantenimientos)
                .FirstOrDefaultAsync(d => d.IdDocumento == id);

            if (documento == null)
                return NotFound();

            var tarea = documento.Tareas.FirstOrDefault();
            var mantenimiento = documento.Mantenimientos.FirstOrDefault();

            var modelo = new DocumentoFormViewModel
            {
                IdDocumento = documento.IdDocumento,
                TipoDocumento = documento.TipoDocumento,
                NumeroDocumento = documento.NumeroDocumento,
                Descripcion = documento.Descripcion,
                FechaEjecucion = documento.FechaEjecucion,
                FechaGeneracion = documento.FechaGeneracion,
                FechaInicio = documento.FechaInicio,
                FechaFin = documento.FechaFin,
                IdEmpresa = documento.IdEmpresa ?? 0,
                Suministro = documento.Suministro ?? false,
                Instalacion = documento.Instalacion ?? false,
                Mantenimiento = documento.Mantenimiento ?? false,
                Soporte = documento.Soporte ?? false,
                CantidadMantenimientos = mantenimiento?.TotalMantenimientos,
                IdTecnicoAsignado = tarea?.IdTecnicoAsignado,
                IdColaboradorAsignado = tarea?.IdColaboradorAsignado,
                ArchivoUrlActual = documento.ArchivoUrl,
                CotizacionArchivoUrlActual = documento.CotizacionArchivoUrl
            };

            await CargarListasFormularioAsync(modelo.IdTecnicoAsignado, modelo.IdColaboradorAsignado, modelo.IdEmpresa);
            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, DocumentoFormViewModel modelo)
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario") ?? 0;
            var rol = HttpContext.Session.GetInt32("Rol");

            if (idUsuario == 0 || rol == null)
                return RedirectToAction("Login", "Auth");

            if (rol == 3)
                return Forbid();

            if (id != modelo.IdDocumento)
                return BadRequest("El identificador del pendiente no coincide.");

            var documento = await _context.Documentos
                .Include(d => d.Tareas)
                .Include(d => d.Mantenimientos)
                .Include(d => d.MaintenanceSchedules)
                .FirstOrDefaultAsync(d => d.IdDocumento == id);

            if (documento == null)
                return NotFound();

            if (string.IsNullOrEmpty(modelo.TipoDocumento) ||
                !(new[] { "Contrato", "Orden", "Otro" }.Contains(modelo.TipoDocumento)))
            {
                ModelState.AddModelError("TipoDocumento", "Tipo de documento inválido.");
            }

            if (modelo.TipoDocumento == "Contrato")
            {
                if (modelo.FechaInicio == null || modelo.FechaFin == null)
                    ModelState.AddModelError("", "Debes ingresar fecha de inicio y fin para un contrato.");

                if (modelo.FechaInicio.HasValue && modelo.FechaFin.HasValue && modelo.FechaFin.Value < modelo.FechaInicio.Value)
                    ModelState.AddModelError("FechaFin", "La fecha fin no puede ser anterior a la fecha de inicio.");
            }
            else
            {
                if (modelo.FechaGeneracion == null)
                    ModelState.AddModelError("FechaGeneracion", "La fecha de generación es obligatoria.");
            }

            if (modelo.IdEmpresa <= 0)
                ModelState.AddModelError("IdEmpresa", "Debes seleccionar una empresa.");

            ValidarArchivoPdf(modelo.ArchivoPdf, nameof(modelo.ArchivoPdf));
            ValidarArchivoPdf(modelo.ArchivoCotizacionPdf, nameof(modelo.ArchivoCotizacionPdf));

            if (!ModelState.IsValid)
            {
                modelo.ArchivoUrlActual = documento.ArchivoUrl;
                modelo.CotizacionArchivoUrlActual = documento.CotizacionArchivoUrl;
                await CargarListasFormularioAsync(modelo.IdTecnicoAsignado, modelo.IdColaboradorAsignado, modelo.IdEmpresa);
                return View(modelo);
            }

            documento.TipoDocumento = modelo.TipoDocumento;
            documento.NumeroDocumento = modelo.NumeroDocumento;
            documento.Descripcion = modelo.Descripcion;
            documento.IdEmpresa = modelo.IdEmpresa;
            documento.FechaEjecucion = modelo.FechaEjecucion;
            documento.Suministro = modelo.Suministro;
            documento.Instalacion = modelo.Instalacion;
            documento.Mantenimiento = modelo.Mantenimiento;
            documento.Soporte = modelo.Soporte;

            if (modelo.TipoDocumento == "Contrato")
            {
                documento.FechaInicio = modelo.FechaInicio;
                documento.FechaFin = modelo.FechaFin;
                documento.FechaGeneracion = null;
            }
            else
            {
                documento.FechaGeneracion = modelo.FechaGeneracion;
                documento.FechaInicio = null;
                documento.FechaFin = null;
            }

            if (modelo.ArchivoPdf != null && modelo.ArchivoPdf.Length > 0)
            {
                var archivoAnterior = documento.ArchivoUrl;
                documento.ArchivoUrl = await GuardarArchivoPdfAsync(modelo.ArchivoPdf);
                EliminarArchivoFisico(archivoAnterior);
            }
            else if (modelo.EliminarArchivoActual)
            {
                EliminarArchivoFisico(documento.ArchivoUrl);
                documento.ArchivoUrl = null;
            }

            if (modelo.ArchivoCotizacionPdf != null && modelo.ArchivoCotizacionPdf.Length > 0)
            {
                var cotizacionAnterior = documento.CotizacionArchivoUrl;
                documento.CotizacionArchivoUrl = await GuardarArchivoPdfAsync(modelo.ArchivoCotizacionPdf);
                documento.CotizacionFecha = DateTime.Today;
                EliminarArchivoFisico(cotizacionAnterior);
            }
            else if (modelo.EliminarCotizacionActual)
            {
                EliminarArchivoFisico(documento.CotizacionArchivoUrl);
                documento.CotizacionArchivoUrl = null;
                documento.CotizacionFecha = null;
            }

            var tarea = documento.Tareas.FirstOrDefault();
            if (tarea == null && (modelo.IdTecnicoAsignado.HasValue || modelo.IdColaboradorAsignado.HasValue))
            {
                tarea = new Tarea
                {
                    IdDocumento = documento.IdDocumento,
                    FechaAsignacion = DateOnly.FromDateTime(DateTime.Today),
                    Estado = "pendiente",
                    Completada = false
                };
                _context.Tareas.Add(tarea);
            }

            if (tarea != null)
            {
                tarea.IdTecnicoAsignado = modelo.IdTecnicoAsignado;
                tarea.IdColaboradorAsignado = modelo.IdColaboradorAsignado;
                tarea.FechaAsignacion ??= DateOnly.FromDateTime(DateTime.Today);
                if (string.IsNullOrWhiteSpace(tarea.Estado))
                    tarea.Estado = "pendiente";
            }

            if (modelo.Mantenimiento && (modelo.CantidadMantenimientos ?? 0) > 0)
            {
                var cantidad = modelo.CantidadMantenimientos!.Value;
                var mantenimiento = documento.Mantenimientos.FirstOrDefault();

                if (mantenimiento == null)
                {
                    mantenimiento = new Mantenimiento
                    {
                        IdDocumento = documento.IdDocumento,
                        MantenimientoRealizado = 0,
                        ProximoMantenimiento = null,
                        FechasRealizadasJson = JsonSerializer.Serialize(new List<string>())
                    };
                    _context.Mantenimientos.Add(mantenimiento);
                }

                mantenimiento.TotalMantenimientos = cantidad;

                if (!documento.MaintenanceSchedules.Any())
                {
                    short seq = 1;
                    foreach (var fecha in CalcularFechasMantenimiento(modelo, cantidad))
                    {
                        _context.MaintenanceSchedules.Add(new MaintenanceSchedule
                        {
                            DocumentoId = documento.IdDocumento,
                            Seq = seq++,
                            PlannedDate = fecha,
                            IsCompleted = false,
                            Notified7d = false,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Exito"] = "Pendiente actualizado correctamente.";
            return RedirectToAction(nameof(Detalle), new { id = documento.IdDocumento });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearCliente(SubirPendienteClienteViewModel model)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            if (rol != 3) return Forbid();

            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");
            var idUsuarioSubio = HttpContext.Session.GetInt32("IdUsuario") ?? 0;

            // Fijar empresa desde sesión si no viene
            if (!model.IdEmpresa.HasValue && idEmpresaSesion.HasValue)
                model.IdEmpresa = idEmpresaSesion.Value;

            if (!ModelState.IsValid)
            {
                // Reponer lista de empresas si aplica
                if (model.Empresas == null || !model.Empresas.Any())
                {
                    model.Empresas = await _context.Empresas
                        .OrderBy(e => e.Nombre)
                        .Select(e => new SelectListItem { Value = e.IdEmpresa.ToString(), Text = e.Nombre })
                        .ToListAsync();
                }
                return View("SubirPendienteCliente", model);
            }

            // Armar descripción unificada tipo webhook
            var sb = new StringBuilder();
            sb.AppendLine("Solicitud enviada por cliente:");
            sb.AppendLine($"• Contacto: {model.NombreCompleto} – {model.NumeroContacto}");
            sb.AppendLine($"• Ubicación: {model.Ubicacion}");
            sb.AppendLine("• Descripción:");
            sb.AppendLine(model.Descripcion?.Trim() ?? "");

            // 🔢 Consecutivo solo para 'Otro'
            var siguiente = await GenerarConsecutivoOtroAsync();

            var documento = new Documento
            {
                // NumeroDocumento: no lo seteamos aquí para que siga tu consecutivo actual
                TipoDocumento = "Otro",
                NumeroDocumento = siguiente,
                Descripcion = sb.ToString(),
                IdEmpresa = model.IdEmpresa,

                // Solo estos servicios (Suministro forzado a false)
                Suministro = false,
                Instalacion = model.Instalacion,
                Mantenimiento = model.Mantenimiento,
                Soporte = model.Soporte,

                // Fechas y estado base
                FechaSubida = DateOnly.FromDateTime(DateTime.Today),
                FechaGeneracion = DateOnly.FromDateTime(DateTime.Today),
                FechaEjecucion = null,

                Asignada = false,
                IdUsuarioSubio = idUsuarioSubio
            };

            _context.Documentos.Add(documento);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Pendiente enviado. El equipo revisará tu solicitud.";

            // ===== Email: nuevo pendiente =====
            const int ROL_ADMIN = 1;

            var creador = await _context.Usuarios
                .Where(u => u.IdUsuario == idUsuarioSubio)
                .Select(u => new { u.Nombre, u.Correo })
                .FirstOrDefaultAsync();

            var adminMails = await _context.Usuarios
                .Where(u => u.IdRol == ROL_ADMIN && u.Estado == "activo" && u.Correo != null && u.Correo != "")
                .Select(u => u.Correo!)
                .ToListAsync();

            var baseUrl = _cfg["PublicBaseUrl"];
            var detalleUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? $"{baseUrl!.TrimEnd('/')}/Dashboard/Detalle/{documento.IdDocumento}"
                : Url.Action("Detalle", "Dashboard", new { id = documento.IdDocumento }, Request.Scheme)!;

            var empresaNombre = await _context.Empresas
                .Where(e => e.IdEmpresa == documento.IdEmpresa)
                .Select(e => e.Nombre)
                .FirstOrDefaultAsync();

            await _email.SendPendienteCreadoAsync(
                new[] { creador?.Correo ?? HttpContext.Session.GetString("Correo") ?? "" }.Concat(adminMails),
                new EmailService.PendienteEmailModel(
                    documento.IdDocumento,
                    documento.NumeroDocumento,
                    documento.TipoDocumento,
                    empresaNombre,
                    creador?.Nombre ?? HttpContext.Session.GetString("Nombre"),
                    documento.FechaSubida,
                    documento.Suministro ?? false,
                    documento.Instalacion ?? false,
                    documento.Mantenimiento ?? false,
                    documento.Soporte ?? false,
                    documento.Descripcion,
                    detalleUrl
                )
            );

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Detalle(int id) // Vista Detalle del Pendiente
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

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
                
            // 🔒 Validación: clientes solo acceden a documentos de su empresa
            if (rol == 3 && idEmpresaSesion.HasValue && documento.IdEmpresa != idEmpresaSesion.Value)
            {
                return Forbid(); // o RedirectToAction("Index") si prefieres
            }

            var viewModel = new DetalleDocumentoViewModel
            {
                Documento = documento,
                EmpresaNombre = documento.IdEmpresaNavigation?.Nombre ?? "Sin empresa",
                Tareas = documento.Tareas.ToList(),
                Materiales = documento.MaterialesPendientes.ToList(),
                UsuarioActual = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.IdUsuario == documento.IdUsuarioSubio) ?? new Usuario()
            };

            ViewBag.EsCliente = (rol == 3);
            return View(viewModel);
        }

        [HttpGet("Dashboard/Mantenimientos/{id:int}")]
        [HttpGet("Dashboard/{id:int}/Mantenimientos")]
        public async Task<IActionResult> Mantenimientos(int id)
        {
            if ((HttpContext.Session.GetInt32("IdUsuario") ?? 0) == 0)
                return Unauthorized();
            
            var rol = HttpContext.Session.GetInt32("Rol");
            var rows = await _context.MaintenanceSchedules
                .Where(m => m.DocumentoId == id)
                .OrderBy(m => m.Seq)
                .ToListAsync();
            ViewBag.DocId = id;
            ViewBag.EsCliente = (rol == 3);
            return PartialView("~/Views/Dashboard/_MaintenancePanel.cshtml", rows);
        }

        [IgnoreAntiforgeryToken]
        [HttpPost("Dashboard/Mantenimientos/{docId:int}/{id:int}/fecha")]
        [HttpPost("Dashboard/{docId:int}/Mantenimientos/{id:int}/fecha")]
        public async Task<IActionResult> UpdatePlanned(int docId, int id, [FromForm] DateTime plannedDate)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            if ((HttpContext.Session.GetInt32("IdUsuario") ?? 0) == 0) return Unauthorized();
            if (rol == 3) return Forbid(); // clientes solo ven, no editan
            
            var m = await _context.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == id && x.DocumentoId == docId);
            if (m == null) return NotFound();
            m.PlannedDate = plannedDate.Date;
            m.Notified7d  = false;
            m.UpdatedAt   = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [IgnoreAntiforgeryToken]
        [HttpPost("Dashboard/Mantenimientos/{docId:int}/{id:int}/completar")]
        [HttpPost("Dashboard/{docId:int}/Mantenimientos/{id:int}/completar")]
        public async Task<IActionResult> Complete(int docId, int id, [FromForm] DateTime completedAt)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            if ((HttpContext.Session.GetInt32("IdUsuario") ?? 0) == 0) return Unauthorized();
            if (rol == 3) return Forbid(); // clientes solo ven, no editan
            
            var m = await _context.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == id && x.DocumentoId == docId);
            if (m == null) return NotFound();
            m.IsCompleted = true;
            m.CompletedAt = completedAt;
            m.UpdatedAt   = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [IgnoreAntiforgeryToken]
        [HttpPost("Dashboard/Mantenimientos/{docId:int}/reordenar")]
        [HttpPost("Dashboard/{docId:int}/Mantenimientos/reordenar")]
        public async Task<IActionResult> Resequence(int docId)
        {
            var list = await _context.MaintenanceSchedules
                .Where(x => x.DocumentoId == docId)
                .OrderBy(x => x.PlannedDate)
                .ToListAsync();
            short i = 1;
            foreach (var m in list) { m.Seq = i++; m.UpdatedAt = DateTime.UtcNow; }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("/Dashboard/Mantenimientos/{docId:int}/bulk")]
        [HttpPost("/Dashboard/{docId:int}/Mantenimientos/bulk")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaintenanceBulk(int docId, [FromForm] DateTime[] plannedDates)
        {
            if (plannedDates == null || plannedDates.Length == 0)
                return BadRequest("Debes enviar al menos una fecha.");

            var dates = plannedDates
                .Where(d => d != default)
                .Select(d => d.Date)
                .OrderBy(d => d)
                .ToList();

            // continuar numeración
            short lastSeq = (short)((await _context.MaintenanceSchedules
                .Where(x => x.DocumentoId == docId)
                .MaxAsync(x => (short?)x.Seq)) ?? 0);

            foreach (var d in dates)
            {
                _context.MaintenanceSchedules.Add(new MaintenanceSchedule
                {
                    DocumentoId = docId,
                    Seq = ++lastSeq,
                    PlannedDate = d,
                    IsCompleted = false,
                    Notified7d = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            var list = await _context.MaintenanceSchedules
                .Where(x => x.DocumentoId == docId)
                .OrderBy(x => x.Seq)
                .ToListAsync();

            ViewBag.DocId = docId;
            return RedirectToAction("Detalle", "Dashboard", new { id = docId });
        }

        // === Guardar/editar FECHA PROGRAMADA ===
        [HttpPost("Dashboard/Mantenimientos/Planificar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Planificar(int id, DateTime? fecha)
        {
            // rol 3 (cliente) no puede editar
            var rol = HttpContext.Session.GetInt32("Rol");
            if (rol == 3) return Forbid();

            var m = await _context.MaintenanceSchedules.FindAsync(id);
            if (m == null) return NotFound();

            // normalizamos al día (sin hora)
            m.PlannedDate = fecha?.Date;

            // si cambias la fecha, vuelve a permitir recordatorio
            m.Notified7d = false;
            m.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // === Marcar COMPLETADO (o reabrir) ===
        [HttpPost("Dashboard/Mantenimientos/Completar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Completar(int id, DateTime? fechaReal, bool? reopen)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            if (rol == 3) return Forbid();

            var m = await _context.MaintenanceSchedules.FindAsync(id);
            if (m == null) return NotFound();

            if (reopen == true)
            {
                m.IsCompleted = false;
                m.CompletedAt = null;
            }
            else
            {
                m.IsCompleted = true;
                m.CompletedAt = (fechaReal ?? DateTime.UtcNow).Date;
            }

            m.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarFechaEjecucion(int idDocumento, DateTime? fechaEjecucion)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            if (rol == 3) return Forbid();  // ⛔ clientes no pueden modificar
            
            if (fechaEjecucion == null)
            {
                TempData["Error"] = "Debes seleccionar una fecha válida.";
                return RedirectToAction("Detalle", new { id = idDocumento });
            }

            var documento = await _context.Documentos.FindAsync(idDocumento);
            if (documento == null)
            {
                TempData["Error"] = "Documento no encontrado.";
                return RedirectToAction("Dashboard");
            }

            documento.FechaEjecucion = fechaEjecucion;
            await _context.SaveChangesAsync();

            TempData["Exito"] = "Fecha de ejecución actualizada correctamente.";
            return RedirectToAction("Detalle", new { id = idDocumento });
        }
    
        [HttpPost]
        public async Task<IActionResult> CambiarEstadoTarea(int idTarea, string nuevoEstado)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var tarea = await _context.Tareas
                .Include(t => t.IdDocumentoNavigation)
                .FirstOrDefaultAsync(t => t.IdTarea == idTarea);

            if (tarea == null) return NotFound();

            // 🔒 Validación
            if (rol == 3) return Forbid();

            // ✅ Lógica normal
            tarea.Estado = nuevoEstado;

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
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
                return NotFound();

            // 🔒 Validación: si es cliente solo puede ver documentos de su empresa
            if (rol == 3 && documento.IdEmpresa != idEmpresaSesion)
                return Forbid();

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
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var documento = await _context.Documentos.FindAsync(idDocumento);
            if (documento == null)
                return NotFound();

            // 🔒 Validación: si es cliente solo puede modificar documentos de su empresa
            if (rol == 3) return Forbid();

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
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
                return NotFound();

            // 🔒 Validación: si es cliente solo puede ver documentos de su empresa
            if (rol == 3 && documento.IdEmpresa != idEmpresaSesion)
                return Forbid();

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
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var documento = await _context.Documentos
                .Include(d => d.Tareas)
                .FirstOrDefaultAsync(d => d.IdDocumento == idDocumento);

            if (documento == null)
                return NotFound();

            // 🔒 Validación: si es cliente solo puede modificar documentos de su empresa
            if (rol == 3) return Forbid();

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
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var mantenimiento = await _context.Mantenimientos
                .Include(m => m.IdDocumentoNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mantenimiento == null)
                return NotFound();

            // 🔒 Validación: si es cliente solo puede ver mantenimientos de su empresa
            if (rol == 3 && mantenimiento.IdDocumentoNavigation.IdEmpresa != idEmpresaSesion)
                return Forbid();

            return View(mantenimiento);
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarMantenimientoPost(int id, DateTime? proxima) // Fechas de los Mantenimientos
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var mantenimiento = await _context.Mantenimientos
                .Include(m => m.IdDocumentoNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mantenimiento == null)
                return NotFound();

            // 🔒 Validación: si es cliente solo puede modificar mantenimientos de su empresa
            if (rol == 3) return Forbid();

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
        public async Task<IActionResult> ActualizarProximoMantenimiento(int id, DateOnly? nuevaFecha) // Proxima Fecha
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var mantenimiento = await _context.Mantenimientos
                .Include(m => m.IdDocumentoNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mantenimiento == null)
                return NotFound();

            // 🔒 Validación: si es cliente solo puede modificar mantenimientos de su empresa
            if (rol == 3) return Forbid();

            mantenimiento.ProximoMantenimiento = nuevaFecha;
            await _context.SaveChangesAsync();

            return RedirectToAction("Detalle", new { id = mantenimiento.IdDocumento });
        }

        [HttpGet]
        public async Task<IActionResult> Historial(string? empresa, string? estado, string? tipo)
        {
            var rol = HttpContext.Session.GetInt32("Rol");
            var idEmpresaSesion = HttpContext.Session.GetInt32("IdEmpresa");

            var query = _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas)
                    .ThenInclude(t => t.IdTecnicoAsignadoNavigation)
                .Include(d => d.IdEmpresaNavigation)
                .AsQueryable();

            // Solo documentos con tareas cerradas (Completado o Cancelado)
            query = query.Where(d => d.Tareas.Any(t => t.Estado == "Completado" || t.Estado == "Cancelado"));

            // 🔒 Si es cliente, limitar a documentos de su empresa
            if (rol == 3 && idEmpresaSesion.HasValue)
            {
                query = query.Where(d => d.IdEmpresa == idEmpresaSesion.Value);
            }

            // 🔍 Filtro por empresa (solo si NO es cliente)
            if (rol != 3 && !string.IsNullOrEmpty(empresa))
            {
                query = query.Where(d => d.IdEmpresaNavigation.Nombre == empresa);
            }

            // 🔍 Filtro por estado (solo completado/cancelado)
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

            // 👇 Ordenar primero por FechaCierre (últimos arriba)
            query = query
                .OrderByDescending(d => d.FechaCierre)
                .ThenByDescending(d => d.FechaSubida);

            var documentos = await query.ToListAsync();

            var modelo = documentos.Select(d => new DashboardItemViewModel
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
                TecnicoAsignado = d.Tareas
                    .FirstOrDefault(t => t.IdTecnicoAsignadoNavigation != null)
                    ?.IdTecnicoAsignadoNavigation?.Nombre ?? "No asignado",
                EmpresaNombre = d.IdEmpresaNavigation?.Nombre ?? "Sin empresa",
            }).ToList();

            // 📌 Preparar ViewBag para filtros
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

            // Empresa seleccionada
            ViewBag.EmpresaSeleccionada = empresa;

            // Flag para la vista
            ViewBag.EsCliente = (rol == 3);

            // Si es cliente, obtener el nombre de su empresa
            if (rol == 3 && idEmpresaSesion.HasValue && string.IsNullOrEmpty(empresa))
            {
                var empresaCliente = await _context.Empresas
                    .Where(e => e.IdEmpresa == idEmpresaSesion.Value)
                    .Select(e => e.Nombre)
                    .FirstOrDefaultAsync();

                ViewBag.EmpresaSeleccionada = empresaCliente;
            }

            return View(modelo);
        }
    }
}