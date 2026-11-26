
namespace Inventario.Api.Models
{
    public class UnidadMedida
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Abreviatura { get; set; } = string.Empty;

        public bool PermiteFracciones { get; set; }
    }
}

