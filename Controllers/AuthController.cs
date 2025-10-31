using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Organizacional.Data;
using Organizacional.Models;
using Organizacional.Models.ViewModels;
using Organizacional.Services;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;

namespace Organizacional.Controllers
{
    public class AuthController : Controller
    {
        private readonly OrganizacionalContext _context;
        private readonly EmailService _email;

        public AuthController(OrganizacionalContext context, EmailService email)
        {
            _context = context;
            _email = email;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            Console.WriteLine("[Auth] GET Login ejecutado");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel modelo, string? returnUrl)
        {
            Console.WriteLine($"[Auth] Login POST correo={modelo.Correo}");
            if (!ModelState.IsValid)
            {
                Console.WriteLine("[Auth] ModelState inválido");
                return View(modelo);
            }

            var correo = (modelo.Correo ?? "").Trim();
            var pass   = (modelo.Contrasena ?? "").Trim();

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == correo && u.Estado == "activo");

            if (usuario == null || (usuario.Contrasena ?? "") != pass)
            {
                ModelState.AddModelError("", "Credenciales inválidas.");
                ViewBag.ReturnUrl = returnUrl;
                return View(modelo);
            }

            // Quitar ReturnUrl
            HttpContext.Session.Remove("PendingReturnUrl");

            HttpContext.Session.SetInt32("IdUsuario", usuario.IdUsuario);

            // Compatibilidad: algunos lugares leen "Rol", otros "IdRol"
            int rol = usuario.IdRol ?? 0;
            HttpContext.Session.SetInt32("Rol",    rol);
            HttpContext.Session.SetInt32("IdRol",  rol);

            HttpContext.Session.SetString("Nombre", usuario.Nombre ?? "");
            HttpContext.Session.SetString("Correo", usuario.Correo ?? "");

            if (usuario.IdEmpresa.HasValue)
                HttpContext.Session.SetInt32("IdEmpresa", usuario.IdEmpresa.Value);
            else
                HttpContext.Session.Remove("IdEmpresa");

            // Emitir cookie de autenticación
            var claims = new List<Claim> {
                new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nombre ?? ""),
                new Claim(ClaimTypes.Email, usuario.Correo ?? ""),
                new Claim(ClaimTypes.Role, (usuario.IdRol ?? 0).ToString()),
            };
            if (usuario.IdEmpresa.HasValue)
                claims.Add(new Claim("IdEmpresa", usuario.IdEmpresa.Value.ToString()));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
            
            // Redirigir al dashboard
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        public IActionResult KeepAlive()
        {
            // Refresca la sesión en el servidor guardando algo
            HttpContext.Session.SetString("LastPing", DateTime.Now.ToString("O"));

            return Ok(new { success = true, message = "Sesión renovada" });
        }

        [HttpGet]
        public IActionResult OlvidoContrasena()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> OlvidoContrasena(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                ModelState.AddModelError("", "Debes escribir tu correo.");
                return View();
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);
            // Mensaje neutro para no revelar existencia del correo
            if (usuario == null)
            {
                TempData["Mensaje"] = "Si el correo existe, enviaremos instrucciones para restablecer la contraseña.";
                return RedirectToAction("OlvidoContrasena");
            }

            // Generar token (2 horas)
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            usuario.TokenRecuperacion = token;
            usuario.TokenExpira = DateTime.UtcNow.AddHours(2);
            usuario.DebeCambiarContrasena = true;
            await _context.SaveChangesAsync();

            var link = Url.Action("CambiarContrasenaInicial", "Auth",
                new { id = usuario.IdUsuario, token },
                Request.Scheme)!;

            await _email.SendForgotPasswordAsync(usuario.Correo!, usuario.Nombre ?? "", link);

            TempData["Mensaje"] = "Si el correo existe, enviamos un enlace para restablecer la contraseña.";
            return RedirectToAction("Login");
        }

        [HttpGet("Auth/CambiarContrasenaInicial")]
        [AllowAnonymous]
        public async Task<IActionResult> CambiarContrasenaInicial(int id, string token)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u =>
                u.IdUsuario == id && u.TokenRecuperacion == token && u.TokenExpira > DateTime.UtcNow);

            if (usuario == null)
            {
                TempData["Mensaje"] = "El enlace no es válido o ha expirado. Pide una nueva invitación.";
                return RedirectToAction("Login");
            }

            var modelo = new CambiarContrasenaViewModel
            {
                IdUsuario = id,
                Token = token,
                Correo = usuario.Correo ?? ""
            };
            return View(modelo);
        }

        [HttpPost("Auth/CambiarContrasenaInicial")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasenaInicial([Bind("IdUsuario,Token,NuevaContrasena,ConfirmarContrasena")] CambiarContrasenaViewModel model)
        {
            // Evitar que Email estorbe en este flujo
            ModelState.Remove(nameof(CambiarContrasenaViewModel.Correo));

            if (!ModelState.IsValid) return View(model);

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u =>
                u.IdUsuario == model.IdUsuario && u.TokenRecuperacion == model.Token && u.TokenExpira > DateTime.UtcNow);

            if (usuario == null)
            {
                TempData["Mensaje"] = "El enlace no es válido o ha expirado. Pide una nueva invitación.";
                return RedirectToAction("Login");
            }

            if (model.NuevaContrasena != model.ConfirmarContrasena)
            {
                ModelState.AddModelError("ConfirmarContrasena", "Las contraseñas no coinciden.");
                return View(model);
            }

            // TODO: si más adelante hasheas, aplica hash aquí
            usuario.Contrasena = model.NuevaContrasena;
            usuario.DebeCambiarContrasena = false;
            usuario.TokenRecuperacion = null;
            usuario.TokenExpira = null;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Contraseña definida. Ahora puedes ingresar.";
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Limpia toda la sesión
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            // Redirige al Login
            return RedirectToAction("Login", "Auth");
        }

        [Authorize]
        [HttpGet("/debug/me")]
        public IActionResult Me()
        {
            var dict = new Dictionary<string, object?> {
                ["Auth"] = new {
                    IsAuth = User.Identity?.IsAuthenticated,
                    Name = User.Identity?.Name,
                    Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                },
                ["Session"] = new {
                    IdUsuario = HttpContext.Session.GetInt32("IdUsuario"),
                    Rol = HttpContext.Session.GetInt32("Rol"),
                    IdRol = HttpContext.Session.GetInt32("IdRol"),
                    IdEmpresa = HttpContext.Session.GetInt32("IdEmpresa"),
                    Correo = HttpContext.Session.GetString("Correo"),
                    Nombre = HttpContext.Session.GetString("Nombre"),
                }
            };
            return new JsonResult(dict);
        }
    }
}