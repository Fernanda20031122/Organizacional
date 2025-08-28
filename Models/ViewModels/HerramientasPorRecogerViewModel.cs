namespace Organizacional.Models.ViewModels
{
    public class HerramientasPorRecogerViewModel
    {
        public int Id { get; set; }

        // Número de documento asociado a la herramienta
        public string NumeroDocumento { get; set; } = "";

        //Empresa a la que se le dejó la herramienta
        public string EmpresaDestino { get; set; } = "";

        // Persona responsable de la herramienta
        public string TecnicoAsignado { get; set; } = "";
        public string NombreUsuario { get; set; } = "";
        public string DejadaPor { get; set; } = "";

        // Dónde se encuentra la herramienta
        public string UbicacionDejado { get; set; } = "";

        // Cuándo se registró la herramienta
        public DateTime? FechaRegistro { get; set; }

        // Si ya fue recogida o no
        public bool Pendiente { get; set; }

        // Cuándo se recogió (nullable porque puede estar pendiente)
        public DateTime? FechaRecogida { get; set; }
        public string Tipo { get; set; } = "";
        public bool Suministro { get; set; }
        public bool Instalacion { get; set; }
        public bool Mantenimiento { get; set; }
        public bool Soporte { get; set; }
    }
}               
