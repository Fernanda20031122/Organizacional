using System;
using System.Collections.Generic;

namespace Organizacional.Models;

public partial class Tarea
{
    public int IdTarea { get; set; }

    public int IdDocumento { get; set; }

    public int? IdTecnicoAsignado { get; set; }

    public DateOnly? FechaAsignacion { get; set; }

    public DateOnly? FechaEjecucion { get; set; }

    public string Estado { get; set; } = "Pendiente";

    public bool? Completada { get; set; }

    public virtual Documento IdDocumentoNavigation { get; set; } = null!;

    public virtual Usuario? IdTecnicoAsignadoNavigation { get; set; }
}
