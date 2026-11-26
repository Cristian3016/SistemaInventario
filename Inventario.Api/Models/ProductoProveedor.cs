
namespace Inventario.Api.Models
{
    public class ProductoProveedor
    {
        public int Id { get; set; }

        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;

        public int ProveedorId { get; set; }

        public Proveedor Proveedor { get; set; } = null!;
    }
}
