using System.ComponentModel.DataAnnotations.Schema;

namespace Organizacional.Models.ViewModels
{
    public class MaterialesPorEntregarViewModel
    {
        public int IdPendiente { get; set; }
        public string NumeroDocumento { get; set; } = "";
        public string EmpresaNombre { get; set; } = "";
        public DateTime? FechaRegistro { get; set; }
        public string TecnicoAsignado { get; set; } = "";
        public string Tipo { get; set; } = "";
        public bool Suministro { get; set; }
        public bool Instalacion { get; set; }
        public bool Mantenimiento { get; set; }
        public bool Soporte { get; set; }
    }
}
