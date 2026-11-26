
namespace Inventario.Api.Models
{
    public class SesionUsuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int UsuarioId { get; set; }

        public Usuario Usuario { get; set; } = null!;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public DateTime UltimaActividad { get; set; } = DateTime.UtcNow;

        public DateTime FechaExpiracion { get; set; }

        public DateTime? FechaRevocacion { get; set; }

        public string? MotivoRevocacion { get; set; }

        public string? DireccionIp { get; set; }

        public string? UserAgent { get; set; }
    }
}

