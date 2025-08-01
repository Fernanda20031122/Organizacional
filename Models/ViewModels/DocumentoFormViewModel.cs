﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Organizacional.Models.ViewModels
{
    public class DocumentoFormViewModel
    {
        // Datos generales del documento
        [Required]
        public string TipoDocumento { get; set; }

        public string? NumeroDocumento { get; set; }

        public string? Descripcion { get; set; }

        public DateOnly? FechaGeneracion { get; set; }

        public DateOnly? FechaInicio { get; set; }

        public DateOnly? FechaFin { get; set; }

        public string? EmpresaDestino { get; set; }

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

        // Lista de técnicos disponibles (opcional)
        // Esta lista se puede llenar desde el controlador
        // public List<SelectListItem>? TecnicosDisponibles { get; set; }
        
        // Lista de colaboradores disponibles (opcional)
        // Esta lista se puede llenar desde el controlador

       // public List<SelectListItem>? TecnicosDisponibles { get; set; }

        // Archivos
        public IFormFile? ArchivoPdf { get; set; }

        public IFormFile? ArchivoCotizacionPdf { get; set; }
    }
}
