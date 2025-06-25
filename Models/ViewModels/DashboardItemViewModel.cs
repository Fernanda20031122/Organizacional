using System;

namespace Organizacional.Models.ViewModels
{
    public class DashboardItemViewModel
    {
        public int? IdTarea { get; set; }
        public int IdDocumento { get; set; }
        public string Tipo { get; set; } = "";
        public string NumeroDocumento { get; set; } = "";
        public string Estado { get; set; } = "";
        public string SubidoPor { get; set; } = "";
        public DateTime FechaSubida { get; set; }
        public int DiasTranscurridos { get; set; }
        public int DiasTotalesContrato { get; set; }
        public int DiasTranscurridosContrato { get; set; }
        public int PorcentajeProgreso { get; set; }
        public string TecnicoAsignado { get; set; } = "";
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Servicios { get; set; } = "";
        public bool Suministro { get; set; }
        public bool Instalacion { get; set; }
        public bool Mantenimiento { get; set; }
    }
}