
namespace Inventario.Api.DTOs
{
    public class RegistrarPagoVentaDto
    {
        public int MetodoPagoId { get; set; }

        public decimal Monto { get; set; }

        public string? ReferenciaOperacion { get; set; }
    }
}
