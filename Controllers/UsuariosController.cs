using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;
using Organizacional.Services;           // <-- AÑADE
using System.Security.Cryptography;      // <-- AÑADE
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Organizacional.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly OrganizacionalContext _context;
        private readonly EmailService _email;      // <-- AÑADE

        public UsuariosController(OrganizacionalContext context, EmailService email)
        {
            _context = context;
            _email = email;
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
                model.Roles = _context.Roles
                    .Select(r => new SelectListItem { Value = r.IdRol.ToString(), Text = r.NombreRol })
                    .ToList();
                model.Empresas = _context.Empresas
                    .Select(e => new SelectListItem { Value = e.IdEmpresa.ToString(), Text = e.Nombre })
                    .ToList();
                return View(model);
            }

            if (await _context.Usuarios.AnyAsync(u => u.Correo == model.Correo))
            {
                ModelState.AddModelError("Correo", "Ya existe un usuario con este correo.");
                model.Roles = _context.Roles
                    .Select(r => new SelectListItem { Value = r.IdRol.ToString(), Text = r.NombreRol })
                    .ToList();
                model.Empresas = _context.Empresas
                    .Select(e => new SelectListItem { Value = e.IdEmpresa.ToString(), Text = e.Nombre })
                    .ToList();
                return View(model);
            }

            // ======= AQUÍ creamos el usuario y lo dejamos en 'usuario' (scope correcto) =======
            var usuario = new Usuario
            {
                Nombre = model.Nombre,
                Correo = model.Correo,
                Contrasena = null,                // sin contraseña; la define con el link
                IdRol = model.IdRol,
                IdEmpresa = model.IdEmpresa,
                Estado = "activo",
                DebeCambiarContrasena = true,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();    // necesitamos IdUsuario

            // Token de invitación (24h)
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            usuario.TokenRecuperacion = token;
            usuario.TokenExpira = DateTime.UtcNow.AddHours(24);
            await _context.SaveChangesAsync();

            // Link absoluto (mejor usa Url.Action para no concatenar a mano)
            var link = Url.Action(
                action: "CambiarContrasenaInicial",
                controller: "Auth",
                values: new { id = usuario.IdUsuario, token },
                protocol: Request.Scheme
            )!;

            await _email.SendInviteAsync(usuario.Correo!, usuario.Nombre ?? "", link);

            TempData["Mensaje"] = "Usuario creado. Se envió un correo de invitación para definir la contraseña.";
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