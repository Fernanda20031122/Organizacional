using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;

namespace Organizacional.Controllers
{
    public class AuthController : Controller
    {
        private readonly OrganizacionalContext _context;

        public AuthController(OrganizacionalContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == model.Correo && u.Contrasena == model.Contrasena);

            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
                return View(model);
            }
            if (usuario.Estado != "activo")
            {
                ModelState.AddModelError("", "Usuario inactivo");
                return View();
            }

            if (usuario.DebeCambiarContrasena == true)
            {
                return RedirectToAction("CambiarContrasenaInicial", new { id = usuario.IdUsuario });
            }

            // Guardar datos en sesión
            HttpContext.Session.SetString("NombreUsuario", usuario.Nombre ?? "");
            HttpContext.Session.SetInt32("IdUsuario", usuario.IdUsuario);
            HttpContext.Session.SetInt32("Rol", usuario.IdRol?? 0);

            // Redirigir por rol
            if (usuario.IdRol == 1) return RedirectToAction("Index", "Dashboard"); // Admin
            if (usuario.IdRol == 2) return RedirectToAction("Index", "Tareas");    // Técnico

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult OlvidoContrasena()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> OlvidoContrasena(string correo)
        {
            if (string.IsNullOrEmpty(correo))
            {
                ModelState.AddModelError("", "Debes ingresar un correo.");
                return View();
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);
            if (usuario == null)
            {
                ModelState.AddModelError("", "Correo no encontrado.");
                return View();
            }

            // Marcar para cambio de contraseña
            usuario.DebeCambiarContrasena = true;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Se ha marcado tu cuenta para cambiar contraseña. Ingresa normalmente para continuar.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> CambiarContrasenaInicial(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            var modelo = new CambiarContrasenaViewModel
            {
                IdUsuario = id,
                Correo = usuario.Correo ?? ""
            };

            return View(modelo);
        }

        [HttpPost]
        public async Task<IActionResult> CambiarContrasenaInicial(CambiarContrasenaViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.IdUsuario == 0)
            {
                ModelState.AddModelError("", "ID de usuario no válido.");
                return View(model);
            }

            var usuario = await _context.Usuarios.FindAsync(model.IdUsuario);
            if (usuario == null)
            {
                ModelState.AddModelError("", "Usuario no encontrado.");
                return View(model);
            }

            if (usuario.DebeCambiarContrasena != true)
            {
                TempData["Mensaje"] = "⚠️ Este usuario no requiere cambiar la contraseña.";
                return RedirectToAction("Login");
            }

            if (model.NuevaContrasena != model.ConfirmarContrasena)
            {
                ModelState.AddModelError("ConfirmarContrasena", "Las contraseñas no coinciden.");
                return View(model);
            }

            // 🔒 Aquí se realiza el cambio
            usuario.Contrasena = model.NuevaContrasena;
            usuario.DebeCambiarContrasena = false;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "✅ Contraseña actualizada correctamente.";
            return RedirectToAction("Login");
        }
    }
}