using System;
using System.Collections.Generic;

namespace Organizacional.Models;

public partial class SedesDocumento
{
    public int Id { get; set; }

    public int IdDocumento { get; set; }

    public string? Direccion { get; set; }

    public virtual Documento IdDocumentoNavigation { get; set; } = null!;
}
