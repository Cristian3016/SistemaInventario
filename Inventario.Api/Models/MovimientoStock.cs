
namespace Inventario.Api.Models
{
    public enum TipoMovimiento
    {
        Entrada = 1,
        Salida = 2
    }

    public class MovimientoStock
    {
        public int Id { get; set; }

        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;

        public TipoMovimiento Tipo { get; set; }

        public decimal Cantidad { get; set; }

        public string Observacion { get; set; } = string.Empty;

        public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    }
}

