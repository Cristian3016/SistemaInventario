using Inventario.Api.Data;
using Inventario.Api.Models;

namespace Inventario.Api.Services
{
    public class AuditoriaService : IAuditoriaService
    {
        private readonly InventarioDbContext _context;

        public AuditoriaService(InventarioDbContext context)
        {
            _context = context;
        }

        public void Registrar(
            int? usuarioId,
            string accion,
            string entidad,
            string? entidadId = null,
            string? descripcion = null)
        {
            var auditoria = new Auditoria
            {
                UsuarioId = usuarioId,
                Accion = accion,
                Entidad = entidad,
                EntidadId = entidadId,
                Descripcion = descripcion,
                FechaUtc = DateTime.UtcNow
            };

            _context.Auditorias.Add(auditoria);
        }
    }
}