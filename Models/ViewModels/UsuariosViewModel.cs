using Microsoft.AspNetCore.Mvc.Rendering;

namespace Organizacional.Models.ViewModels
{
    public class UsuarioViewModel
    {
        public string Nombre { get; set; } = "";
        public string Correo { get; set; } = "";
        public int IdRol { get; set; }
        public int? IdEmpresa { get; set; }

        public List<SelectListItem>? Roles { get; set; }
        public List<SelectListItem>? Empresas { get; set; }
    }
}
