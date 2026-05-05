using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Organizacional.Models.ViewModels
{
    public class DocumentoFormViewModel
    {
        public int IdDocumento { get; set; }

        // Datos generales del documento
        [Required]
        public string TipoDocumento { get; set; }

        public string? NumeroDocumento { get; set; }

        public string? Descripcion { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        public DateOnly? FechaGeneracion { get; set; }

        public DateOnly? FechaInicio { get; set; }

        public DateOnly? FechaFin { get; set; }

        public string? EmpresaDestino { get; set; }
        public int IdEmpresa { get; set; }

        // Tipos de servicio
        public bool Suministro { get; set; }
        public bool Instalacion { get; set; }
        public bool Mantenimiento { get; set; }
        public bool Soporte { get; set; }

        // Mantenimiento
        public int? CantidadMantenimientos { get; set; }

        public string? PeriodicidadMantenimientos { get; set; } // ejemplo: "30" días

        // Técnico asignado (opcional)
       public int? IdTecnicoAsignado { get; set; }
       public int? IdColaboradorAsignado { get; set; }

        // Archivos actuales, usados en la pantalla de edición
        public string? ArchivoUrlActual { get; set; }
        public string? CotizacionArchivoUrlActual { get; set; }
        public bool EliminarArchivoActual { get; set; }
        public bool EliminarCotizacionActual { get; set; }

        // Archivos nuevos
        public IFormFile? ArchivoPdf { get; set; }

        public IFormFile? ArchivoCotizacionPdf { get; set; }
    }
}
