
namespace Inventario.Api.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SesionUsuarioId { get; set; }

        public SesionUsuario SesionUsuario { get; set; } = null!;

        public string TokenHash { get; set; } = string.Empty;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public DateTime FechaExpiracion { get; set; }

        public DateTime? FechaUtilizacion { get; set; }

        public DateTime? FechaRevocacion { get; set; }

        public string? MotivoRevocacion { get; set; }

        public Guid? ReemplazadoPorId { get; set; }
    }
}

