
namespace Inventario.Api.Models
{
    public class PagoVenta
    {
        public int Id { get; set; }

        public int VentaId { get; set; }

        public Venta Venta { get; set; } = null!;

        public int MetodoPagoId { get; set; }

        public MetodoPago MetodoPago { get; set; } = null!;

        public int CajaId { get; set; }

        public Caja Caja { get; set; } = null!;

        public decimal Monto { get; set; }

        public string? ReferenciaOperacion { get; set; }

        public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    }
}
