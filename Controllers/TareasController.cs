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

        // GET: Tareas
        public async Task<IActionResult> Index()
        {
            var organizacionalContext = _context.Tareas.Include(t => t.IdDocumentoNavigation).Include(t => t.IdTecnicoAsignadoNavigation);
            return View(await organizacionalContext.ToListAsync());
        }

        // GET: Tareas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tarea = await _context.Tareas
                .Include(t => t.IdDocumentoNavigation)
                .Include(t => t.IdTecnicoAsignadoNavigation)
                .FirstOrDefaultAsync(m => m.IdTarea == id);
            if (tarea == null)
            {
                return NotFound();
            }

            return View(tarea);
        }

        // GET: Tareas/Create
        public IActionResult Create()
        {
            ViewData["IdDocumento"] = new SelectList(_context.Documentos, "IdDocumento", "IdDocumento");
            ViewData["IdTecnicoAsignado"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario");
            return View();
        }

        // POST: Tareas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdTarea,IdDocumento,IdTecnicoAsignado,FechaAsignacion,FechaEjecucion,Estado")] Tarea tarea)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tarea);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdDocumento"] = new SelectList(_context.Documentos, "IdDocumento", "IdDocumento", tarea.IdDocumento);
            ViewData["IdTecnicoAsignado"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario", tarea.IdTecnicoAsignado);
            return View(tarea);
        }

        // GET: Tareas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea == null)
            {
                return NotFound();
            }
            ViewData["IdDocumento"] = new SelectList(_context.Documentos, "IdDocumento", "IdDocumento", tarea.IdDocumento);
            ViewData["IdTecnicoAsignado"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario", tarea.IdTecnicoAsignado);
            return View(tarea);
        }

        // POST: Tareas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdTarea,IdDocumento,IdTecnicoAsignado,FechaAsignacion,FechaEjecucion,Estado")] Tarea tarea)
        {
            if (id != tarea.IdTarea)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tarea);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TareaExists(tarea.IdTarea))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdDocumento"] = new SelectList(_context.Documentos, "IdDocumento", "IdDocumento", tarea.IdDocumento);
            ViewData["IdTecnicoAsignado"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario", tarea.IdTecnicoAsignado);
            return View(tarea);
        }

        // GET: Tareas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tarea = await _context.Tareas
                .Include(t => t.IdDocumentoNavigation)
                .Include(t => t.IdTecnicoAsignadoNavigation)
                .FirstOrDefaultAsync(m => m.IdTarea == id);
            if (tarea == null)
            {
                return NotFound();
            }

            return View(tarea);
        }

        // POST: Tareas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea != null)
            {
                _context.Tareas.Remove(tarea);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TareaExists(int id)
        {
            return _context.Tareas.Any(e => e.IdTarea == id);
        }
    }
}
