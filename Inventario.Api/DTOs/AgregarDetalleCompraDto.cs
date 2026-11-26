
namespace Inventario.Api.DTOs
{
    public class AgregarDetalleCompraDto
    {
        public int ProductoId { get; set; }

        public decimal Cantidad { get; set; }

        public decimal PrecioUnitario { get; set; }
    }
}
