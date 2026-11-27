namespace Inventario.Api.Services
{
    public interface IAuditoriaService
    {
        void Registrar(
            int? usuarioId,
            string accion,
            string entidad,
            string? entidadId = null,
            string? descripcion = null);
    }
}