
namespace Inventario.Api.DTOs
{
    public class ProductoPatchDto
    {
        public string? Nombre { get; set; }

        public string? Descripcion { get; set; }

        public string? CodigoSku { get; set; }

        public decimal? Precio { get; set; }

        public int? Stock { get; set; }

        public int? StockMinimo { get; set; }

        public int? CategoriaId { get; set; }
    }
}
