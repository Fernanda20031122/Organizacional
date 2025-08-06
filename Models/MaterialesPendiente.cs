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
        [Column("IdDocumento")]  // Esto asegura que EF use el nombre correcto
        public int IdDocumento { get; set; }

        [ForeignKey("IdDocumento")]
        public Documento Documento { get; set; }

        [Required]
        [StringLength(255)]
        public string NombreMaterial { get; set; }

        [StringLength(255)]
        public string? NombreHerramientaDejada { get; set; }

        [StringLength(255)]
        public string? UbicacionDejado { get; set; }

        [Required]
        public bool EsSolicitado { get; set; }
        public bool MaterialEntregado { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
