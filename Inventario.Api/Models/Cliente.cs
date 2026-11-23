
namespace Inventario.Api.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        public string NombreRazonSocial { get; set; } = string.Empty;

        public string? TipoDocumento { get; set; }

        public string? NumeroDocumento { get; set; }

        public string? Email { get; set; }

        public string? Telefono { get; set; }

        public string? Direccion { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
