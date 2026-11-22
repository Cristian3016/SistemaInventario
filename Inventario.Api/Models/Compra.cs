
namespace Inventario.Api.Models
{
    public class Compra
    {
        public int Id { get; set; }

        public int ProveedorId { get; set; }

        public Proveedor Proveedor { get; set; } = null!;

        public DateTime FechaCompra { get; set; } = DateTime.UtcNow;

        public string? NumeroComprobante { get; set; }

        public string Estado { get; set; } = "Borrador";

        public decimal Total { get; set; } = 0m;
    }
}
