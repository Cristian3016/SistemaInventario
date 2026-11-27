using Inventario.Api.Authorization;
using Inventario.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Controllers
{
    [ApiController]
    [Route("api/auditoria")]
    public class AuditoriaController : ControllerBase
    {
        private readonly InventarioDbContext _context;

        public AuditoriaController(InventarioDbContext context)
        {
            _context = context;
        }

        [Permiso(Permisos.AuditoriaVer)]
        [HttpGet]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerAuditorias(
            [FromQuery] int? usuarioId = null,
            [FromQuery] string? accion = null,
            [FromQuery] string? entidad = null,
            [FromQuery] string? entidadId = null,
            [FromQuery] DateTimeOffset? desde = null,
            [FromQuery] DateTimeOffset? hasta = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20)
        {
            if (usuarioId.HasValue && usuarioId.Value <= 0)
                return BadRequest(
                    "El ID del usuario debe ser mayor que cero.");

            if (pagina <= 0)
                return BadRequest(
                    "La página debe ser mayor que cero.");

            if (tamanoPagina < 1 || tamanoPagina > 100)
                return BadRequest(
                    "El tamaño de página debe estar entre 1 y 100.");

            var desplazamiento = (pagina - 1L) * tamanoPagina;

            if (desplazamiento > int.MaxValue)
                return BadRequest(
                    "El número de página supera el límite permitido.");

            accion = accion?.Trim().ToUpperInvariant();
            entidad = entidad?.Trim();
            entidadId = entidadId?.Trim();

            if (accion?.Length > 100)
                return BadRequest(
                    "La acción no puede superar los 100 caracteres.");

            if (entidad?.Length > 100)
                return BadRequest(
                    "La entidad no puede superar los 100 caracteres.");

            if (entidadId?.Length > 100)
                return BadRequest(
                    "El identificador de entidad no puede superar los 100 caracteres.");

            if (desde.HasValue &&
                hasta.HasValue &&
                desde.Value >= hasta.Value)
            {
                return BadRequest(
                    "La fecha desde debe ser anterior a la fecha hasta.");
            }

            var consulta = _context.Auditorias.AsNoTracking();

            if (usuarioId.HasValue)
            {
                consulta = consulta.Where(a =>
                    a.UsuarioId == usuarioId.Value);
            }

            if (!string.IsNullOrWhiteSpace(accion))
            {
                consulta = consulta.Where(a =>
                    a.Accion == accion);
            }

            if (!string.IsNullOrWhiteSpace(entidad))
            {
                consulta = consulta.Where(a =>
                    a.Entidad == entidad);
            }

            if (!string.IsNullOrWhiteSpace(entidadId))
            {
                consulta = consulta.Where(a =>
                    a.EntidadId == entidadId);
            }

            if (desde.HasValue)
            {
                var fechaDesde = desde.Value.UtcDateTime;

                consulta = consulta.Where(a =>
                    a.FechaUtc >= fechaDesde);
            }

            if (hasta.HasValue)
            {
                var fechaHasta = hasta.Value.UtcDateTime;

                consulta = consulta.Where(a =>
                    a.FechaUtc < fechaHasta);
            }

            var totalRegistros = await consulta
                .CountAsync(HttpContext.RequestAborted);

            var totalPaginas = (int)Math.Ceiling(
                totalRegistros / (double)tamanoPagina);

            var auditorias = await consulta
                .OrderByDescending(a => a.FechaUtc)
                .ThenByDescending(a => a.Id)
                .Skip((int)desplazamiento)
                .Take(tamanoPagina)
                .Select(a => new
                {
                    a.Id,
                    a.UsuarioId,
                    a.Accion,
                    a.Entidad,
                    a.EntidadId,
                    a.Descripcion,
                    a.FechaUtc
                })
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(new
            {
                pagina,
                tamanoPagina,
                totalRegistros,
                totalPaginas,
                tienePaginaAnterior = pagina > 1,
                tienePaginaSiguiente = pagina < totalPaginas,
                registros = auditorias.Select(a => new
                {
                    a.Id,
                    a.UsuarioId,
                    a.Accion,
                    a.Entidad,
                    a.EntidadId,
                    a.Descripcion,
                    FechaUtc = DateTime.SpecifyKind(
                        a.FechaUtc,
                        DateTimeKind.Utc)
                })
            });
        }
    }
}
