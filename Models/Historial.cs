using System;
using System.Collections.Generic;

namespace Organizacional.Models;

public partial class Historial
{
    public int IdHistorial { get; set; }

    public int IdUsuario { get; set; }

    public string Accion { get; set; } = null!;

    public int IdDocumento { get; set; }

    public DateTime Fecha { get; set; }

    public virtual Documento IdDocumentoNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
