
using System.ComponentModel.DataAnnotations;

namespace Inventario.Api.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(50, MinimumLength = 4,
            ErrorMessage = "El nombre de usuario debe contener entre 4 y 50 caracteres.")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(128,
            ErrorMessage = "La contraseña no puede superar los 128 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }
}
