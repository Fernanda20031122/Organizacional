using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Organizacional.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public string? Nombre { get; set; }

    public string? Correo { get; set; }

    public string? Contrasena { get; set; }

    public int? IdRol { get; set; }

    public string? Estado { get; set; }
    
    [Column("token_expira")]
    public DateTime? TokenExpira { get; set; }

    [Column("token_recuperacion")]
    public string? TokenRecuperacion { get; set; }

    // 🔹 Relación con Empresa (nuevo)public int? IdEmpresa { get; set; }

    public int? IdEmpresa { get; set; }

    [ForeignKey("IdEmpresa")]
    public virtual Empresa? Empresa { get; set; }

    [NotMapped]
    public string? ContrasenaTemporal { get; set; }
    public bool? DebeCambiarContrasena { get; set; } = true;

    public DateTime? FechaCreacion { get; set; }

    public virtual ICollection<Documento> Documentos { get; set; } = new List<Documento>();

    public virtual ICollection<Historial> Historials { get; set; } = new List<Historial>();

    public virtual Role? IdRolNavigation { get; set; }

    public virtual ICollection<Tarea> Tareas { get; set; } = new List<Tarea>();
}
