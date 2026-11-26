
namespace Inventario.Api.Models
{
    public class Venta
    {
        public int Id { get; set; }

        public int? ClienteId { get; set; }

        public Cliente? Cliente { get; set; }

        public DateTime FechaVenta { get; set; } = DateTime.UtcNow;

        public string? NumeroComprobante { get; set; }

        public string Estado { get; set; } = "Borrador";

        public decimal Total { get; set; } = 0m;
    }
}
