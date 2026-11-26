
namespace Inventario.Api.Models
{
    public enum TipoMovimientoCaja
    {
        Ingreso = 1,
        Egreso = 2
    }
    public class MovimientoCaja
    {
        public int Id { get; set; }

        public int CajaId { get; set; }

        public Caja Caja { get; set; } = null!;

        public TipoMovimientoCaja Tipo { get; set; }

        public decimal Monto { get; set; }

        public string Concepto { get; set; } = string.Empty;

        public string? Observacion { get; set; }

        public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    }
}
