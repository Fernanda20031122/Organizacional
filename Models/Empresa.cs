using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Organizacional.Models
{
    [Table("empresas")]
    public class Empresa
    {
        [Key]
        public int IdEmpresa { get; set; }

        [Required]
        [StringLength(255)]
        public string Nombre { get; set; } = "";

    }
}
