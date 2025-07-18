using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Organizacional.Models
{
    public class MaterialesPendiente
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdTarea { get; set; }

        [Required]
        [StringLength(255)]
        public string NombreMaterial { get; set; }

        [StringLength(255)]
        public string? UbicacionDejado { get; set; }

        [Required]
        public bool EsSolicitado { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Relación con la tarea
        [ForeignKey("IdTarea")]
        public virtual Tarea Tarea { get; set; }
    }
}
