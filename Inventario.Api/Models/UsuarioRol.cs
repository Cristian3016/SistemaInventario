
namespace Inventario.Api.Models
{
    public class UsuarioRol
    {
        public int UsuarioId { get; set; }

        public Usuario Usuario { get; set; } = null!;

        public int RolId { get; set; }

        public Rol Rol { get; set; } = null!;

        public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;
    }
}

