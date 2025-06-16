using System;
using System.Collections.Generic;

namespace Organizacional.Models;

public partial class Documento
{
    public int IdDocumento { get; set; }

    public string TipoDocumento { get; set; } = null!;

    public string? NumeroDocumento { get; set; }

    public string? Descripcion { get; set; }

    public DateOnly? FechaGeneracion { get; set; }

    public DateOnly? FechaSubida { get; set; }

    public string? ArchivoUrl { get; set; }

    public int? IdUsuarioSubio { get; set; }

    public bool? Asignada { get; set; }

    public string? EmpresaDestino { get; set; }

    public bool? Suministro { get; set; }

    public bool? Instalacion { get; set; }

    public bool? Mantenimiento { get; set; }

    public DateOnly? FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    public string? CotizacionArchivoUrl { get; set; }

    public DateTime? CotizacionFecha { get; set; }

    public virtual ICollection<Historial> Historials { get; set; } = new List<Historial>();

    public virtual Usuario? IdUsuarioSubioNavigation { get; set; }

    public virtual ICollection<Mantenimiento> Mantenimientos { get; set; } = new List<Mantenimiento>();

    public virtual ICollection<SedesDocumento> SedesDocumentos { get; set; } = new List<SedesDocumento>();

    public virtual ICollection<Tarea> Tareas { get; set; } = new List<Tarea>();
}
