using System.ComponentModel.DataAnnotations;

namespace Organizacional.Models.ViewModels
{
    public class CambiarContrasenaViewModel
    {
        public int IdUsuario { get; set; }
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string NuevaContrasena { get; set; } = String.Empty;

        [Required(ErrorMessage = "Debe confirmar la contraseña.")]
        [DataType(DataType.Password)]
        [Compare("NuevaContrasena", ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarContrasena { get; set; } = String.Empty;
    }
}