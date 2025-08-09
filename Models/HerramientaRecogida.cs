using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Organizacional.Models
{
    [Table("HerramientasRecogidas")]
    public class HerramientaRecogida
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [ForeignKey("Documento")]
        [Column("IdDocumento")]
        public int IdDocumento { get; set; }

        [ForeignKey("Usuario")]
        [Column("IdUsuario")]
        public int IdUsuario { get; set; }

        [Required]
        [Column("NombreHerramienta")]
        public string NombreHerramienta { get; set; }

        [Column("UbicacionDejado")]
        public string? UbicacionDejado { get; set; }

        [Column("FechaRegistro")]
        public DateTime FechaRegistro { get; set; }
        public bool Recogida { get; set; }

        // Relaciones
        public Documento? Documento { get; set; }

        [ForeignKey("IdUsuario")]
        public Usuario IdUsuarioNavigation { get; set; }

    }
}
