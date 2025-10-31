using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Organizacional.Models.ViewModels
{
    public class SubirPendienteClienteViewModel
    {
        // Empresa puede venir fija desde sesión o seleccionarse
        public int? IdEmpresa { get; set; }
        public List<SelectListItem>? Empresas { get; set; }

        [Required, Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = "";

        [Required, Display(Name = "Número de contacto")]
        public string NumeroContacto { get; set; } = "";

        [Required, Display(Name = "Ubicación del problema")]
        public string Ubicacion { get; set; } = "";

        [Required, Display(Name = "Descripción del problema")]
        public string Descripcion { get; set; } = "";

        // Solo estos 3 checkboxes
        public bool Instalacion { get; set; }
        public bool Mantenimiento { get; set; }
        public bool Soporte { get; set; }
    }
}