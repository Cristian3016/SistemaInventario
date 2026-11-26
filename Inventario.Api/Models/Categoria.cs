
namespace Inventario.Api.Models
{
    public class Categoria
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public int? CategoriaPadreId { get; set; }

        public Categoria? CategoriaPadre { get; set; }

        public ICollection<Categoria> Subcategorias { get; set; }
            = new List<Categoria>();

        public ICollection<Producto> Productos { get; set; }
            = new List<Producto>();
    }
}

