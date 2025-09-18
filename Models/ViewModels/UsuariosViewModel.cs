using Microsoft.AspNetCore.Mvc.Rendering;

namespace Organizacional.Models.ViewModels
{
    public class UsuarioViewModel
    {
        public string Nombre { get; set; } = "";
        public string Correo { get; set; } = "";
        public string Contrasena { get; set; } = "";
        public int IdRol { get; set; }

        // Opcional solo para Cliente
        public int? IdEmpresa { get; set; }

        // Listas para selects
        public List<SelectListItem>? Roles { get; set; }
        public List<SelectListItem>? Empresas { get; set; }
    }
}
