namespace Inventario.Api.Models
{
    public class Auditoria
    {
        public long Id { get; set; }

        public int? UsuarioId { get; set; }

        public string Accion { get; set; } = string.Empty;

        public string Entidad { get; set; } = string.Empty;

        public string? EntidadId { get; set; }

        public string? Descripcion { get; set; }

        public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    }
}