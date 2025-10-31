using System.ComponentModel.DataAnnotations;

namespace Organizacional.Models.ViewModels
{
    public class CambiarContrasenaViewModel
    {
        public int IdUsuario { get; set; }

        // lo usamos para validar el enlace de invitación
        public string Token { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Debes ingresar un correo válido.")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [Display(Name = "Nueva contraseña")]
        public string NuevaContrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debes confirmar la contraseña.")]
        [DataType(DataType.Password)]
        [Compare("NuevaContrasena", ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarContrasena { get; set; } = string.Empty;
    }
}
