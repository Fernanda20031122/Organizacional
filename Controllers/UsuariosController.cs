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

        // Solo visible para administrador
        [HttpGet]
        public IActionResult CrearAdministrador()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearAdministrador(Usuario modelo)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Usuarios.AnyAsync(u => u.Correo == modelo.Correo))
                {
                    ModelState.AddModelError("Correo", "Ya existe un usuario con este correo.");
                    return View(modelo);
                }

                modelo.Contrasena = modelo.Contrasena; // O encriptada si se aplica
                modelo.DebeCambiarContrasena = true;
                modelo.Estado = "activo";
                modelo.FechaCreacion = DateTime.Now;
                modelo.IdRol = 1; // 1 = Administrador

                _context.Usuarios.Add(modelo);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Administrador creado correctamente.";
                return RedirectToAction("Index");
            }

            return View(modelo);
        }
        [HttpGet]
        public IActionResult CrearTecnico()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTecnico(Usuario modelo)
        {
            if (ModelState.IsValid)
            {
                // Verifica que no exista ese correo ya registrado
                if (await _context.Usuarios.AnyAsync(u => u.Correo == modelo.Correo))
                {
                    ModelState.AddModelError("Correo", "Ya existe un usuario con este correo.");
                    return View(modelo);
                }

                modelo.Contrasena = modelo.Contrasena; // se puede encriptar si usas hash
                modelo.DebeCambiarContrasena = true;
                modelo.Estado = "activo";
                modelo.FechaCreacion = DateTime.Now;
                modelo.IdRol = 2; // Técnico

                _context.Usuarios.Add(modelo);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Técnico creado con éxito. El usuario debe cambiar su contraseña al ingresar.";
                return RedirectToAction("Index", "Usuarios");
            }

            return View(modelo);
        }

        // Aquí podrías listar todos los técnicos
        public async Task<IActionResult> Index()
        {
            var usuarios = await _context.Usuarios.Include(u => u.IdRolNavigation).ToListAsync();
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