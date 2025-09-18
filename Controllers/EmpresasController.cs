using Microsoft.AspNetCore.Mvc;
using Organizacional.Data;
using Organizacional.Models;
using System.Linq;

public class EmpresasController : Controller
{
    private readonly OrganizacionalContext _context;

    public EmpresasController(OrganizacionalContext context)
    {
        _context = context;
    }

    // GET: /Empresas
    public IActionResult Index()
    {
        var empresas = _context.Empresas.ToList();
        return View(empresas);
    }

    // GET: /Empresas/Crear
    public IActionResult Crear()
    {
        return View();
    }

    // POST: /Empresas/Crear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Crear(Empresa empresa)
    {
        if (ModelState.IsValid)
        {
            _context.Empresas.Add(empresa);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(empresa);
    }

    // GET: /Empresas/Editar/5
    public IActionResult Editar(int id)
    {
        var empresa = _context.Empresas.Find(id);
        if (empresa == null)
        {
            return NotFound();
        }
        return View(empresa);
    }

    // POST: /Empresas/Editar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(int id, Empresa empresa)
    {
        if (id != empresa.IdEmpresa)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            _context.Update(empresa);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(empresa);
    }

    // GET: /Empresas/Eliminar/5
    public IActionResult Eliminar(int id)
    {
        var empresa = _context.Empresas.Find(id);
        if (empresa == null)
        {
            return NotFound();
        }
        return View(empresa);
    }

    // POST: /Empresas/Eliminar/5
    [HttpPost, ActionName("Eliminar")]
    [ValidateAntiForgeryToken]
    public IActionResult EliminarConfirmado(int id)
    {
        var empresa = _context.Empresas.Find(id);
        if (empresa != null)
        {
            _context.Empresas.Remove(empresa);
            _context.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }
}