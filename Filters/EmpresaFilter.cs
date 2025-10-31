using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;

namespace Organizacional.Filters
{
    public class EmpresaFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
                return;
            // Excluir rutas de autenticación para no interferir
            var path = context.HttpContext.Request.Path.Value ?? "";
            if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Auth", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        
            var rol = context.HttpContext.Session.GetInt32("Rol");
            var idEmpresa = context.HttpContext.Session.GetInt32("IdEmpresa");

            // Si es cliente (Rol = 3) y no tiene empresa, lo saco
            if (rol == 3 && !idEmpresa.HasValue)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No hacemos nada después de la acción
        }
    }
}
