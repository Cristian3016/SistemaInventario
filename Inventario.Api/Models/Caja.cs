
namespace Inventario.Api.Models
{
    public class Caja
    {
        public int Id { get; set; }

        public DateTime FechaApertura { get; set; } = DateTime.UtcNow;

        public DateTime? FechaCierre { get; set; }

        public decimal MontoInicial { get; set; }

        public decimal? MontoEsperado { get; set; }

        public decimal? MontoReal { get; set; }

        public decimal? Diferencia { get; set; }

        public string Estado { get; set; } = "Abierta";

        public string? Observaciones { get; set; }
    }
}
