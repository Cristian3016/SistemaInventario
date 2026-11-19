
using Inventario.Api.Models;

namespace Inventario.Api.DTOs
{
    public class RegistrarMovimientoStockDto
    {
        public int ProductoId { get; set; }

        public TipoMovimiento Tipo { get; set; }

        public int Cantidad { get; set; }

        public string Observacion { get; set; } = string.Empty;
    }
}

