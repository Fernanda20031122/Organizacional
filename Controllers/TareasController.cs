using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;

namespace Organizacional.Controllers
{
    public class TareasController : Controller
    {
        private readonly OrganizacionalContext _context;

        public TareasController(OrganizacionalContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null) return RedirectToAction("Login", "Auth");

            var tareas = await _context.Tareas
                .Include(t => t.IdDocumentoNavigation)
                .Where(t => t.IdTecnicoAsignado == idUsuario)
                .ToListAsync();

            return View(tareas);
        }
        public async Task<IActionResult> Detalle(int id)
        {
            var documento = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .Include(d => d.Tareas)
                .Include(d => d.Mantenimientos)
                .FirstOrDefaultAsync(d => d.IdDocumento == id);

            if (documento == null) return NotFound();

            return View(documento);
        }
    }
}