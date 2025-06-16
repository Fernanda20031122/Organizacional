using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Organizacional.Models;

public partial class Mantenimiento
{
    public int Id { get; set; }

    public int? IdDocumento { get; set; }

    public int? TotalMantenimientos { get; set; }

    public int? MantenimientoRealizado { get; set; }

    public DateOnly? ProximoMantenimiento { get; set; }

    public string? FechasRealizadasJson { get; set; }
    [NotMapped]
    public List<DateTime> FechasRealizadas
    {
        get => string.IsNullOrEmpty(FechasRealizadasJson)
            ? new List<DateTime>()
            : JsonSerializer.Deserialize<List<DateTime>>(FechasRealizadasJson) ?? new List<DateTime>();

        set => FechasRealizadasJson = JsonSerializer.Serialize(value);
    }

    public virtual Documento? IdDocumentoNavigation { get; set; }
}