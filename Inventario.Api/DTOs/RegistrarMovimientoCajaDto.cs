
using Inventario.Api.Models;

namespace Inventario.Api.DTOs
{
    public class RegistrarMovimientoCajaDto
    {
        public TipoMovimientoCaja Tipo { get; set; }

        public decimal Monto { get; set; }

        public string Concepto { get; set; } = string.Empty;

        public string? Observacion { get; set; }
    }
}

