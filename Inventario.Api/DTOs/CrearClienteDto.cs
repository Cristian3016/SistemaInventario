
namespace Inventario.Api.DTOs
{
    public class CrearClienteDto
    {
        public string NombreRazonSocial { get; set; } = string.Empty;

        public string? TipoDocumento { get; set; }

        public string? NumeroDocumento { get; set; }

        public string? Email { get; set; }

        public string? Telefono { get; set; }

        public string? Direccion { get; set; }
    }
}
