using Inventario.Api.DTOs;
namespace Inventario.Api.DTOs
{
    public class ProductoPatchDto
    {
        public string? Nombre { get; set; }

        public string? Descripcion { get; set; }

        public decimal? Precio { get; set; }

        public int? Stock { get; set; }
    }
}