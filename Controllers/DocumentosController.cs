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
    public class DocumentosController : Controller
    {
        private readonly OrganizacionalContext _context;

        public DocumentosController(OrganizacionalContext context)
        {
            _context = context;
        }

        // GET: Documentos
        public async Task<IActionResult> Index()
        {
            var organizacionalContext = _context.Documentos.Include(d => d.IdUsuarioSubioNavigation);
            return View(await organizacionalContext.ToListAsync());
        }

        // GET: Documentos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documento = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .FirstOrDefaultAsync(m => m.IdDocumento == id);
            if (documento == null)
            {
                return NotFound();
            }

            return View(documento);
        }

        // GET: Documentos/Create
        public IActionResult Create()
        {
            ViewData["IdUsuarioSubio"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario");
            return View();
        }

        // POST: Documentos/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdDocumento,TipoDocumento,NumeroDocumento,Descripcion,FechaGeneracion,FechaSubida,ArchivoUrl,IdUsuarioSubio,Asignada,EmpresaDestino,Suministro,Instalacion,Mantenimiento,FechaInicio,FechaFin")] Documento documento)
        {
            if (ModelState.IsValid)
            {
                _context.Add(documento);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdUsuarioSubio"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario", documento.IdUsuarioSubio);
            return View(documento);
        }

        // GET: Documentos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documento = await _context.Documentos.FindAsync(id);
            if (documento == null)
            {
                return NotFound();
            }
            ViewData["IdUsuarioSubio"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario", documento.IdUsuarioSubio);
            return View(documento);
        }

        // POST: Documentos/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdDocumento,TipoDocumento,NumeroDocumento,Descripcion,FechaGeneracion,FechaSubida,ArchivoUrl,IdUsuarioSubio,Asignada,EmpresaDestino,Suministro,Instalacion,Mantenimiento,FechaInicio,FechaFin")] Documento documento)
        {
            if (id != documento.IdDocumento)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(documento);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DocumentoExists(documento.IdDocumento))
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
            ViewData["IdUsuarioSubio"] = new SelectList(_context.Usuarios, "IdUsuario", "IdUsuario", documento.IdUsuarioSubio);
            return View(documento);
        }

        // GET: Documentos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var documento = await _context.Documentos
                .Include(d => d.IdUsuarioSubioNavigation)
                .FirstOrDefaultAsync(m => m.IdDocumento == id);
            if (documento == null)
            {
                return NotFound();
            }

            return View(documento);
        }

        // POST: Documentos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var documento = await _context.Documentos.FindAsync(id);
            if (documento != null)
            {
                _context.Documentos.Remove(documento);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DocumentoExists(int id)
        {
            return _context.Documentos.Any(e => e.IdDocumento == id);
        }
    }
}
