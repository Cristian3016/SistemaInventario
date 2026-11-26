
namespace Inventario.Api.DTOs
{
    public class LoginRespuestaDto
    {
        public int UsuarioId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Apellido { get; set; } = string.Empty;

        public string NombreUsuario { get; set; } = string.Empty;

        public Guid SesionId { get; set; }

        public string AccessToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = "Bearer";

        public DateTime FechaExpiracionAccessToken { get; set; }

        public DateTime FechaExpiracionSesion { get; set; }

        public DateTime FechaExpiracionInactividad { get; set; }

        public int AdvertenciaInactividadSegundos { get; set; }
    }
}
