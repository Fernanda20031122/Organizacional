using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Organizacional.Filters
{
    public class EmpresaFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var httpContext = context.HttpContext;
            var rol = httpContext.Session.GetInt32("Rol");
            var idEmpresa = httpContext.Session.GetInt32("IdEmpresa");

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
