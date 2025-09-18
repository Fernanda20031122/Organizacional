using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Organizacional.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly OrganizacionalContext _context;

        public UsuariosController(OrganizacionalContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult CrearUsuario()
        {
            var vm = new UsuarioViewModel
            {
                Roles = _context.Roles.Select(r => new SelectListItem
                {
                    Value = r.IdRol.ToString(),
                    Text = r.NombreRol
                }).ToList(),

                Empresas = _context.Empresas.Select(e => new SelectListItem
                {
                    Value = e.IdEmpresa.ToString(),
                    Text = e.Nombre
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearUsuario(UsuarioViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // volver a cargar selects si hay error
                model.Roles = _context.Roles.Select(r => new SelectListItem
                {
                    Value = r.IdRol.ToString(),
                    Text = r.NombreRol
                }).ToList();

                model.Empresas = _context.Empresas.Select(e => new SelectListItem
                {
                    Value = e.IdEmpresa.ToString(),
                    Text = e.Nombre
                }).ToList();

                return View(model);
            }

            if (await _context.Usuarios.AnyAsync(u => u.Correo == model.Correo))
            {
                ModelState.AddModelError("Correo", "Ya existe un usuario con este correo.");
                return View(model);
            }

            var nuevoUsuario = new Usuario
            {
                Nombre = model.Nombre,
                Correo = model.Correo,
                Contrasena = model.Contrasena, // 🔐 aquí deberías hashear
                IdRol = model.IdRol,
                IdEmpresa = model.IdEmpresa,   // 👈 se asigna el FK, no el nombre
                Estado = "activo",
                DebeCambiarContrasena = true,
                FechaCreacion = DateTime.Now
            };

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Usuario creado correctamente.";
            return RedirectToAction("Index");
        }

        // Aquí podrías listar todos los técnicos
        public async Task<IActionResult> Index()
        {
            var usuarios = await _context.Usuarios
                .Include(u => u.IdRolNavigation)
                .Include(u => u.Empresa) // 👈 agregamos la empresa
                .ToListAsync();

            return View(usuarios);
        }
        [HttpPost]
        public async Task<IActionResult> CambiarEstado(int idUsuario)
        {
            var usuario = await _context.Usuarios.FindAsync(idUsuario);
            if (usuario == null) return NotFound();

            usuario.Estado = usuario.Estado == "activo" ? "inactivo" : "activo";
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Estado del usuario actualizado correctamente.";
            return RedirectToAction("Index");
        }
    }
}