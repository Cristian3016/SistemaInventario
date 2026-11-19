namespace Inventario.Api.Models
{
    public class Producto
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public string? CodigoSku { get; set; }

        public decimal Precio { get; set; }

        public int Stock { get; set; }

        public int StockMinimo { get; set; } = 5;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public int? CategoriaId { get; set; }

        public Categoria? Categoria { get; set; }

        public int? UnidadMedidaId { get; set; }

        public UnidadMedida? UnidadMedida { get; set; }

    }
}

